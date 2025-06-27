using System.Collections.Concurrent;
using ControlR.Libraries.Shared.Dtos.HubDtos.PwshCommandCompletions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace ControlR.Web.Client.Components;

public partial class Terminal : IAsyncDisposable
{
  private readonly Dictionary<string, object> _inputAttributes = new()
  {
    ["autocapitalize"] = "off",
    ["spellcheck"] = "false"
  };
  private readonly string _inputElementId = $"terminal-input-{Guid.NewGuid()}";
  private readonly ConcurrentList<string> _inputHistory = [];
  private bool _enableMultiline;
  private MudTextField<string>? _inputElement;
  private int _inputHistoryIndex;
  private string _inputText = string.Empty;

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

  private int InputLineCount => _enableMultiline ? 6 : 1;

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

  protected override async Task OnAfterRenderAsync(bool firstRender)
  {
    await base.OnAfterRenderAsync(firstRender);

    if (_inputElement is not null && !_taboutPrevented)
    {
      _taboutPrevented = true;
      await JsInterop.PreventTabOut(_inputElementId);
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

    _inputText = replacementText;
  }

  private async Task DisplayCompletions(PwshCompletionMatch[] completionMatches)
  {
    await InvokeAsync(StateHasChanged);
  }

  private async Task GetAllCompletions()
  {
    if (string.IsNullOrWhiteSpace(_lastCompletionInput))
    {
      _lastCompletionInput = _inputText;
      _lastCursorIndex = await JsInterop.GetCursorIndexById(_inputElementId);
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
    await DisplayCompletions(completionResult.Value.CompletionMatches);
  }
  private async Task GetNextCompletion(bool forward)
  {
    try
    {
      if (string.IsNullOrWhiteSpace(_lastCompletionInput))
      {
        _lastCompletionInput = _inputText;
        _lastCursorIndex = await JsInterop.GetCursorIndexById(_inputElementId);
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
    if (string.IsNullOrWhiteSpace(_inputText))
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

      _inputText = _inputText.Trim();
      _inputHistory.Add(_inputText);
      _inputHistoryIndex = _inputHistory.Count;

      var result = await ViewerHub.SendTerminalInput(Device.Id, Id, _inputText);
      if (!result.IsSuccess)
      {
        Snackbar.Add(result.Reason, Severity.Error);
      }

      _inputText = string.Empty;
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

  private async Task OnInputKeyUp(KeyboardEventArgs args)
  {
    if (_inputElement is null)
    {
      return;
    }

    if (!IsCommandCompletionInput(args))
    {
      _lastCompletionInput = null;
    }

    if (!_enableMultiline && args.Key.Equals("ArrowUp", StringComparison.OrdinalIgnoreCase))
    {
      _inputText = GetTerminalHistory(false);
      return;
    }

    if (!_enableMultiline && args.Key.Equals("ArrowDown", StringComparison.OrdinalIgnoreCase))
    {
      _inputText = GetTerminalHistory(true);
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
}