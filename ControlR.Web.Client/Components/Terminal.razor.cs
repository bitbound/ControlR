using System.Collections.Concurrent;
using ControlR.Libraries.Shared.Dtos.HubDtos.PwshCommandCompletions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace ControlR.Web.Client.Components;

public partial class Terminal : IAsyncDisposable
{
  private readonly Dictionary<string, object> _commandInputAttributes = new()
  {
    ["autocapitalize"] = "off",
    ["spellcheck"] = "false",
    ["autocomplete"] = "off"
  };
  private readonly string _commandInputElementId = $"terminal-input-{Guid.NewGuid()}";
  private readonly ConcurrentList<string> _inputHistory = [];
  private MudTextField<string> _commandInputElement = default!;
  private string _commandInputText = string.Empty;

  // Provided by UI.  Never null
  private MudAutocomplete<PwshCompletionMatch> _completionsAutoComplete = default!;

  private PwshCompletionsResponseDto? _currentCompletions;
  private bool _enableMultiline;
  private int _inputHistoryIndex;

  private string? _lastCompletionInput;
  private int _lastCursorIndex;
  private bool _loading = true;
  private bool _taboutPrevented;
  private ElementReference _terminalOutputContainer;



  [CascadingParameter]
  public required DeviceContentInstance ContentInstance { get; init; }
  [Parameter]
  [EditorRequired]
  public required DeviceViewModel Device { get; init; }

  [Parameter]
  [EditorRequired]
  public required Guid Id { get; init; }

  [Inject]
  public required IJsInterop JsInterop { get; init; }

  [Inject]
  public required ILogger<Terminal> Logger { get; init; }

  [Inject]
  public required IMessenger Messenger { get; init; }

  [Inject]
  public required ISnackbar Snackbar { get; init; }

  [Inject]
  public required IViewerHubConnection ViewerHub { get; init; }

  [Inject]
  public required IDeviceContentWindowStore WindowStore { get; init; }

  private int CommandInputLineCount => _enableMultiline ? 6 : 1;

  private ConcurrentQueue<TerminalOutputDto> Output { get; } = [];

  public async ValueTask DisposeAsync()
  {
    try
    {
      GC.SuppressFinalize(this);
      await ViewerHub.CloseTerminalSession(Device.Id, Id);
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while disposing of terminal session.");
    }
  }

  public async Task OnCompletionInputKeyDown(KeyboardEventArgs args)
  {
    if (args.Key.Equals("Escape", StringComparison.OrdinalIgnoreCase))
    {
      // Clear completions and focus command input
      _currentCompletions = null;
      await _commandInputElement.FocusAsync();
      await InvokeAsync(StateHasChanged);
      return;
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
  }

  protected override async Task OnInitializedAsync()
  {
    try
    {
      await base.OnInitializedAsync();

      Messenger.Register<DtoReceivedMessage<TerminalOutputDto>>(this, HandleTerminalOutputMessage);

      var result = await ViewerHub.CreateTerminalSession(Device.Id, Id);
      if (!result.IsSuccess)
      {
        Snackbar.Add("Failed to start terminal", Severity.Error);
        Logger.LogResult(result);
        WindowStore.Remove(ContentInstance);
      }
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while initializing Terminal component.");
      Snackbar.Add("Terminal initialization error", Severity.Error);
      WindowStore.Remove(ContentInstance);
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
    if (string.IsNullOrWhiteSpace(_lastCompletionInput))
    {
      return;
    }

    if (completion.CompletionMatches.Length == 0)
    {
      Logger.LogInformation("No completions found for input: {Input}", _lastCompletionInput);
      return;
    }

    if (completion.CurrentMatchIndex < 0 ||
        completion.CurrentMatchIndex >= completion.CompletionMatches.Length)
    {
      Logger.LogWarning(
        "Current match index {Index} is out of bounds for completions array of length {Length}.",
        completion.CurrentMatchIndex,
        completion.CompletionMatches.Length);

      Snackbar.Add("Malformed completion data received", Severity.Error);
      return;
    }

    var match = completion.CompletionMatches[completion.CurrentMatchIndex];

    var replacementText = string.Concat(
      _lastCompletionInput[..completion.ReplacementIndex],
      match.CompletionText,
      _lastCompletionInput[(completion.ReplacementIndex + completion.ReplacementLength)..]);

    _commandInputText = replacementText;
  }

  private async Task GetAllCompletions()
  {
    if (string.IsNullOrWhiteSpace(_lastCompletionInput))
    {
      _lastCompletionInput = _commandInputText;
      _lastCursorIndex = await JsInterop.GetCursorIndexById(_commandInputElementId);
      if (_lastCursorIndex < 0)
      {
        Snackbar.Add("Failed to get cursor index for completions", Severity.Error);
        return;
      }
    }

    var completionResult = await ViewerHub.GetPwshCompletions(Device.Id, Id, _lastCompletionInput, _lastCursorIndex, false);
    if (!completionResult.IsSuccess)
    {
      Snackbar.Add(completionResult.Reason, Severity.Error);
      return;
    }

    if (completionResult.Value.CompletionMatches.Length == 0)
    {
      Logger.LogInformation("No completions found for input: {Input}", _lastCompletionInput);
      return;
    }

    Logger.LogInformation("Received {Count} completions for input: {Input}",
      completionResult.Value.CompletionMatches.Length, _lastCompletionInput);

    _currentCompletions = completionResult.Value;
    await InvokeAsync(StateHasChanged);
    await _completionsAutoComplete.OpenMenuAsync();
    await _completionsAutoComplete.FocusAsync();
  }
  private async Task GetNextCompletion(bool forward)
  {
    try
    {
      if (string.IsNullOrWhiteSpace(_lastCompletionInput))
      {
        _lastCompletionInput = _commandInputText;
        _lastCursorIndex = await JsInterop.GetCursorIndexById(_commandInputElementId);
        if (_lastCursorIndex < 0)
        {
          Snackbar.Add("Failed to get cursor index for completions", Severity.Error);
          return;
        }
      }

      var completionResult = await ViewerHub.GetPwshCompletions(
        Device.Id,
        Id,
        _lastCompletionInput,
        _lastCursorIndex,
        forward);

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
    if (_inputHistory.Count == 0)
    {
      return "";
    }

    if (forward && _inputHistoryIndex < _inputHistory.Count)
    {
      _inputHistoryIndex++;
    }
    else if (!forward && _inputHistoryIndex > 0)
    {
      _inputHistoryIndex--;
    }

    if (_inputHistoryIndex < 0 || _inputHistoryIndex >= _inputHistory.Count)
    {
      return "";
    }

    return _inputHistory[_inputHistoryIndex];
  }
  private async Task HandleEnterKeyInput(KeyboardEventArgs args)
  {
    if (string.IsNullOrWhiteSpace(_commandInputText))
    {
      return;
    }

    if (args.CtrlKey || args.ShiftKey)
    {
      return;
    }

    try
    {
      while (_inputHistory.Count > 500)
      {
        _inputHistory.RemoveAt(0);
      }

      _commandInputText = _commandInputText.Trim();
      _inputHistory.Add(_commandInputText);
      _inputHistoryIndex = _inputHistory.Count;

      var result = await ViewerHub.SendTerminalInput(Device.Id, Id, _commandInputText);
      if (!result.IsSuccess)
      {
        Snackbar.Add(result.Reason, Severity.Error);
      }

      _commandInputText = string.Empty;
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

    if (dto.TerminalId != Id)
    {
      return;
    }

    while (Output.Count > 500)
    {
      _ = Output.TryDequeue(out _);
    }

    Output.Enqueue(dto);
    await InvokeAsync(StateHasChanged);

    await JsInterop.ScrollToEnd(_terminalOutputContainer);
  }
  private async Task OnCompletionSelected(PwshCompletionMatch match)
  {
    if (string.IsNullOrWhiteSpace(_lastCompletionInput))
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
      _lastCompletionInput[.._currentCompletions.ReplacementIndex],
      match.CompletionText,
      _lastCompletionInput[(_currentCompletions.ReplacementIndex + _currentCompletions.ReplacementLength)..]);

    _commandInputText = replacementText;

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
      _lastCompletionInput = null;
    }

    if (!_enableMultiline && args.Key.Equals("ArrowUp", StringComparison.OrdinalIgnoreCase))
    {
      _commandInputText = GetTerminalHistory(false);
      return;
    }

    if (!_enableMultiline && args.Key.Equals("ArrowDown", StringComparison.OrdinalIgnoreCase))
    {
      _commandInputText = GetTerminalHistory(true);
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
}