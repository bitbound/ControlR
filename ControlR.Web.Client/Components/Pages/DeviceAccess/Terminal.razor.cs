using ControlR.Libraries.Shared.Dtos.HubDtos.PwshCommandCompletions;
using ControlR.Web.Client.Services.DeviceAccess;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace ControlR.Web.Client.Components.Pages.DeviceAccess;

public partial class Terminal : IAsyncDisposable
{
  private readonly Dictionary<string, object> _commandInputAttributes = new()
  {
    ["autocapitalize"] = "off",
    ["spellcheck"] = "false",
    ["autocomplete"] = "off"
  };
  private readonly string _commandInputElementId = $"terminal-input-{Guid.NewGuid()}";
  private MudTextField<string> _commandInputElement = null!;

  // Provided by UI.  Never null
  private MudAutocomplete<PwshCompletionMatch> _completionsAutoComplete = null!;

  private PwshCompletionsResponseDto? _currentCompletions;
  private bool _loading = true;
  private ElementReference _terminalOutputContainer;
  private bool _taboutPrevented;

  [Inject]
  public required IDeviceAccessState DeviceState { get; init; }

  [Inject]
  public required IJsInterop JsInterop { get; init; }

  [Inject]
  public required ILogger<Terminal> Logger { get; init; }

  [Inject]
  public required IMessenger Messenger { get; init; }

  [Inject]
  public required ISnackbar Snackbar { get; init; }
  [Inject]
  public required ITerminalState TerminalState { get; init; }

  [Inject]
  public required IViewerHubConnection ViewerHub { get; init; }

  private int CommandInputLineCount => TerminalState.EnableMultiline ? 6 : 1;

  public ValueTask DisposeAsync()
  {
    Messenger.UnregisterAll(this);
    GC.SuppressFinalize(this);
    return ValueTask.CompletedTask;
  }

  public async Task OnCompletionInputKeyDown(KeyboardEventArgs args)
  {
    if (args.Key.Equals("Escape", StringComparison.OrdinalIgnoreCase))
    {
      // Clear completions and focus command input
      _currentCompletions = null;
      await _commandInputElement.FocusAsync();
      await InvokeAsync(StateHasChanged);
    }
  }

  protected override async Task OnAfterRenderAsync(bool firstRender)
  {
    await base.OnAfterRenderAsync(firstRender);

    if (_commandInputElement is not null && !_taboutPrevented)
    {
      _taboutPrevented = true;
      await JsInterop.PreventTabOut(_commandInputElementId);
    }

    // If this is the first render and we have existing output, scroll to end after a short delay
    // to ensure the DOM is fully rendered with content.
    if (firstRender && !TerminalState.Output.IsEmpty)
    {
      await Task.Delay(50);
      await JsInterop.ScrollToEnd(_terminalOutputContainer);
    }
  }

  protected override async Task OnInitializedAsync()
  {
    try
    {
      await base.OnInitializedAsync();

      Messenger.Register<DtoReceivedMessage<TerminalOutputDto>>(this, HandleTerminalOutputMessage);

      var result = await ViewerHub.CreateTerminalSession(DeviceState.CurrentDevice.Id, TerminalState.Id);
      if (!result.IsSuccess)
      {
        Snackbar.Add("Failed to start terminal", Severity.Error);
        Logger.LogResult(result);
      }
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while initializing Terminal component.");
      Snackbar.Add("Terminal initialization error", Severity.Error);
    }
    finally
    {
      _loading = false;
    }
  }

  private static string GetOutputColor(TerminalOutputDto output)
  {
    return output.OutputKind switch
    {
      TerminalOutputKind.StandardOutput => "",
      TerminalOutputKind.StandardError => "mud-error-text",
      _ => ""
    };
  }

  private static bool IsCommandCompletionInput(KeyboardEventArgs args)
  {
    if (args.Key.Equals("Tab", StringComparison.OrdinalIgnoreCase) ||
        args.Key.Equals("Shift", StringComparison.OrdinalIgnoreCase))
    {
      return true;
    }

    if (args.CtrlKey && args.Key.Equals(" ", StringComparison.OrdinalIgnoreCase))
    {
      return true;
    }

    return false;
  }
  private void ApplyCompletion(PwshCompletionsResponseDto completion)
  {
    if (string.IsNullOrWhiteSpace(TerminalState.LastCompletionInput))
    {
      return;
    }

    if (completion.CompletionMatches.Length == 0)
    {
      Logger.LogInformation("No completions found for input: {Input}", TerminalState.LastCompletionInput);
      return;
    }

    var match = completion.CompletionMatches[0];

    var replacementText = string.Concat(
      TerminalState.LastCompletionInput[..completion.ReplacementIndex],
      match.CompletionText,
      TerminalState.LastCompletionInput[(completion.ReplacementIndex + completion.ReplacementLength)..]);

    TerminalState.CommandInputText = replacementText;
  }

  private async Task GetAllCompletions()
  {
    if (string.IsNullOrWhiteSpace(TerminalState.LastCompletionInput))
    {
      TerminalState.LastCompletionInput = TerminalState.CommandInputText;
      TerminalState.LastCursorIndex = await JsInterop.GetCursorIndexById(_commandInputElementId);
      if (TerminalState.LastCursorIndex < 0)
      {
        Snackbar.Add("Failed to get cursor index for completions", Severity.Error);
        return;
      }
    }

    // Start with an empty list to collect all completions
    var allMatches = new List<PwshCompletionMatch>();
    var currentPage = 0;
    PwshCompletionsResponseDto? lastResponse = null;

    // Keep requesting pages until we have all completions
    do
    {
      var requestDto = new PwshCompletionsRequestDto(
        DeviceState.CurrentDevice.Id,
        TerminalState.Id,
        TerminalState.LastCompletionInput,
        TerminalState.LastCursorIndex,
        string.Empty, // ViewerConnectionId will be set by server
        Forward: null,
        currentPage,
        PwshCompletionsRequestDto.DefaultPageSize);

      var completionResult = await ViewerHub.GetPwshCompletions(requestDto);

      if (!completionResult.IsSuccess)
      {
        Snackbar.Add(completionResult.Reason, Severity.Error);
        return;
      }

      if (completionResult.Value.TotalCount >= PwshCompletionsResponseDto.MaxRetrievableItems)
      {
        Snackbar.Add($"Too many items to retrieve ({completionResult.Value.TotalCount})", Severity.Warning);
        return;
      }

      lastResponse = completionResult.Value;
      allMatches.AddRange(lastResponse.CompletionMatches);
      currentPage++;

    } while (lastResponse.HasMorePages);

    if (allMatches.Count == 0)
    {
      Logger.LogInformation("No completions found for input: {Input}", TerminalState.LastCompletionInput);
      return;
    }

    Logger.LogInformation("Received {Count} completions for input: {Input}",
      allMatches.Count, TerminalState.LastCompletionInput);

    // Create a combined response with all matches
    _currentCompletions = new PwshCompletionsResponseDto(
      lastResponse.ReplacementIndex,
      lastResponse.ReplacementLength,
      [.. allMatches],
      false, // No more pages since we collected everything
      allMatches.Count,
      0);

    await InvokeAsync(StateHasChanged);
    await _completionsAutoComplete.OpenMenuAsync();
    await _completionsAutoComplete.FocusAsync();
  }
  private async Task GetNextCompletion(bool forward)
  {
    try
    {
      if (string.IsNullOrWhiteSpace(TerminalState.LastCompletionInput))
      {
        TerminalState.LastCompletionInput = TerminalState.CommandInputText;
        TerminalState.LastCursorIndex = await JsInterop.GetCursorIndexById(_commandInputElementId);
        if (TerminalState.LastCursorIndex < 0)
        {
          Snackbar.Add("Failed to get cursor index for completions", Severity.Error);
          return;
        }
      }

      var requestDto = new PwshCompletionsRequestDto(
        DeviceState.CurrentDevice.Id,
        TerminalState.Id,
        TerminalState.LastCompletionInput,
        TerminalState.LastCursorIndex,
        string.Empty, // ViewerConnectionId will be set by server
        forward);

      var completionResult = await ViewerHub.GetPwshCompletions(requestDto);

      if (!completionResult.IsSuccess)
      {
        Snackbar.Add(completionResult.Reason, Severity.Error);
        return;
      }
      ApplyCompletion(completionResult.Value);
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while getting next command completion.");
      Snackbar.Add("An error occurred while getting completions", Severity.Error);
      return;
    }
  }

  private string GetTerminalHistory(bool forward)
  {
    if (TerminalState.InputHistory.Count == 0)
    {
      return "";
    }

    if (forward && TerminalState.InputHistoryIndex < TerminalState.InputHistory.Count)
    {
      TerminalState.InputHistoryIndex++;
    }
    else if (!forward && TerminalState.InputHistoryIndex > 0)
    {
      TerminalState.InputHistoryIndex--;
    }

    if (TerminalState.InputHistoryIndex < 0 || TerminalState.InputHistoryIndex >= TerminalState.InputHistory.Count)
    {
      return "";
    }

    return TerminalState.InputHistory[TerminalState.InputHistoryIndex];
  }
  private async Task HandleEnterKeyInput(KeyboardEventArgs args)
  {
    if (string.IsNullOrWhiteSpace(TerminalState.CommandInputText))
    {
      return;
    }

    if (args.CtrlKey || args.ShiftKey)
    {
      return;
    }

    try
    {
      while (TerminalState.InputHistory.Count > 500)
      {
        TerminalState.InputHistory.RemoveAt(0);
      }

      TerminalState.CommandInputText = TerminalState.CommandInputText.Trim();
      TerminalState.InputHistory.Add(TerminalState.CommandInputText);
      TerminalState.InputHistoryIndex = TerminalState.InputHistory.Count;

      var result = await ViewerHub.SendTerminalInput(DeviceState.CurrentDevice.Id, TerminalState.Id, TerminalState.CommandInputText);
      if (!result.IsSuccess)
      {
        Snackbar.Add(result.Reason, Severity.Error);
      }

      TerminalState.CommandInputText = string.Empty;
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while sending terminal input.");
      Snackbar.Add("An error occurred", Severity.Error);
    }
  }

  private async Task HandleTerminalOutputMessage(object subscriber, DtoReceivedMessage<TerminalOutputDto> message)
  {
    var dto = message.Dto;

    if (dto.TerminalId != TerminalState.Id)
    {
      return;
    }

    while (TerminalState.Output.Count > 500)
    {
      _ = TerminalState.Output.TryDequeue(out _);
    }

    TerminalState.Output.Enqueue(dto);
    await InvokeAsync(StateHasChanged);
    await JsInterop.ScrollToEnd(_terminalOutputContainer);
  }

  private async Task OnCompletionSelected(PwshCompletionMatch match)
  {
    if (string.IsNullOrWhiteSpace(TerminalState.LastCompletionInput))
    {
      Logger.LogWarning("No last completion input available to apply match.");
      return;
    }

    if (_currentCompletions is null)
    {
      Logger.LogWarning("Current completions are null, cannot apply match.");
      return;
    }

    var replacementText = string.Concat(
      TerminalState.LastCompletionInput[.._currentCompletions.ReplacementIndex],
      match.CompletionText,
      TerminalState.LastCompletionInput[(_currentCompletions.ReplacementIndex + _currentCompletions.ReplacementLength)..]);

    TerminalState.CommandInputText = replacementText;

    _currentCompletions = null;
    await _commandInputElement.FocusAsync();
    await InvokeAsync(StateHasChanged);
  }

  private async Task OnInputKeyDown(KeyboardEventArgs args)
  {
    if (_commandInputElement is null)
    {
      return;
    }

    if (!IsCommandCompletionInput(args))
    {
      TerminalState.LastCompletionInput = null;
    }

    if (!TerminalState.EnableMultiline && args.Key.Equals("ArrowUp", StringComparison.OrdinalIgnoreCase))
    {
      await SetCommandInputText(GetTerminalHistory(false));
      return;
    }

    if (!TerminalState.EnableMultiline && args.Key.Equals("ArrowDown", StringComparison.OrdinalIgnoreCase))
    {
      await SetCommandInputText(GetTerminalHistory(true));
      return;
    }

    if (args.Key == "Enter")
    {
      await HandleEnterKeyInput(args);
      return;
    }

    if (args.Key.Equals("Tab", StringComparison.OrdinalIgnoreCase))
    {
      await GetNextCompletion(!args.ShiftKey);
      return;
    }

    if (args.CtrlKey && args.Key.Equals(" ", StringComparison.OrdinalIgnoreCase))
    {
      await GetAllCompletions();
    }
  }

  private async Task<IEnumerable<PwshCompletionMatch>> SearchCompletions(string value, CancellationToken token)
  {
    var _currentMatches = _currentCompletions?.CompletionMatches;

    if (_currentMatches is not { Length: > 0 })
    {
      // If no completions are available, return an empty list
      return await Array.Empty<PwshCompletionMatch>().AsTaskResult();
    }

    if (string.IsNullOrEmpty(value))
    {
      return await _currentMatches.AsTaskResult();
    }

    return await _currentMatches
      .Where(x => x.ListItemText.Contains(value, StringComparison.InvariantCultureIgnoreCase))
      .AsTaskResult();
  }

  private async Task SetCommandInputText(string text)
  {
    await _commandInputElement.SetText(text);
    await Task.Delay(50);
    await _commandInputElement.SelectRangeAsync(text.Length, text.Length);
  }
}