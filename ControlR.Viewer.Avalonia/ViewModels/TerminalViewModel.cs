using System.Collections.Specialized;
using System.Globalization;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using ControlR.Libraries.Api.Contracts.Dtos.HubDtos;
using ControlR.Libraries.Api.Contracts.Dtos.HubDtos.PwshCommandCompletions;
using ControlR.Libraries.Avalonia.Controls.Dialogs;
using ControlR.Libraries.Avalonia.Controls.Snackbar;
using ControlR.Libraries.Messenger.Extensions.Messages;
using ControlR.Viewer.Avalonia.ViewModels.Dialogs;
using ControlR.Viewer.Avalonia.Views.Dialogs;

namespace ControlR.Viewer.Avalonia.ViewModels;

public interface ITerminalViewModel : IViewModelBase
{
  int CommandInputHeight { get; }
  string CommandInputText { get; set; }
  IReadOnlyList<PwshCompletionMatch> CurrentCompletions { get; }
  bool EnableMultiline { get; set; }
  bool HasCompletions { get; }
  bool IsLoading { get; }
  ReadOnlyObservableCollection<TerminalOutputDto> OutputLines { get; }

  Task ApplyCompletion(PwshCompletionMatch match);
  void ClearCompletions();
  Task GetAllCompletions(int caretIndex);
  Task GetNextCompletion(bool forward, int caretIndex);
  string GetTerminalHistory(bool forward);
  Task HandleEnterKeyInput();
  void ResetCompletionState();
  void SetCommandInputText(string text);
  void ShowKeyboardShortcuts();
  Task<bool> TryHandleEscape();
}

public partial class TerminalViewModel : ViewModelBase<TerminalView>, ITerminalViewModel
{
  private readonly ObservableCollection<PwshCompletionMatch> _currentCompletions = [];
  private readonly IDeviceState _deviceState;
  private readonly IDialogProvider _dialogProvider;
  private readonly ILogger<TerminalViewModel> _logger;
  private readonly IMessenger _messenger;
  private readonly ObservableCollection<TerminalOutputDto> _outputLines = [];
  private readonly ISnackbar _snackbar;
  private readonly ITerminalKeyboardShortcutsDialogViewModel _terminalKeyboardShortcutsDialogViewModel;
  private readonly ITerminalState _terminalState;
  private readonly IHubConnection<IViewerHub> _viewerHub;

  private PwshCompletionsResponseDto? _currentCompletionResponse;
  private bool _enableMultiline;
  [ObservableProperty]
  private bool _isLoading = true;

  public TerminalViewModel(
    IDeviceState deviceState,
    IDialogProvider dialogProvider,
    IHubConnection<IViewerHub> viewerHub,
    ILogger<TerminalViewModel> logger,
    IMessenger messenger,
    ISnackbar snackbar,
    ITerminalKeyboardShortcutsDialogViewModel terminalKeyboardShortcutsDialogViewModel,
    ITerminalState terminalState)
  {
    _deviceState = deviceState;
    _dialogProvider = dialogProvider;
    _viewerHub = viewerHub;
    _logger = logger;
    _messenger = messenger;
    _snackbar = snackbar;
    _terminalKeyboardShortcutsDialogViewModel = terminalKeyboardShortcutsDialogViewModel;
    _terminalState = terminalState;

    EnableMultiline = _terminalState.EnableMultiline;
    OutputLines = new ReadOnlyObservableCollection<TerminalOutputDto>(_outputLines);
    CurrentCompletions = new ReadOnlyObservableCollection<PwshCompletionMatch>(_currentCompletions);

    Disposables.Add(_messenger.Register<DtoReceivedMessage<TerminalOutputDto>>(this, HandleTerminalOutputMessage));
  }

  public int CommandInputHeight => EnableMultiline ? 120 : 40;
  public string CommandInputText
  {
    get => _terminalState.CommandInputText;
    set
    {
      if (_terminalState.CommandInputText == value)
      {
        return;
      }

      _terminalState.CommandInputText = value;
      OnPropertyChanged();
    }
  }
  public IReadOnlyList<PwshCompletionMatch> CurrentCompletions { get; }
  public bool EnableMultiline
  {
    get => _enableMultiline;
    set
    {
      if (SetProperty(ref _enableMultiline, value))
      {
        _terminalState.EnableMultiline = value;
        OnPropertyChanged(nameof(CommandInputHeight));
      }
    }
  }
  public bool HasCompletions => _currentCompletions.Count > 0;
  public ReadOnlyObservableCollection<TerminalOutputDto> OutputLines { get; }

  public async Task ApplyCompletion(PwshCompletionMatch match)
  {
    if (string.IsNullOrWhiteSpace(_terminalState.LastCompletionInput))
    {
      _logger.LogWarning("No last completion input available to apply match.");
      return;
    }

    if (_currentCompletionResponse is null)
    {
      _logger.LogWarning("Current completions are null, cannot apply match.");
      return;
    }

    var replacementText = string.Concat(
      _terminalState.LastCompletionInput[.._currentCompletionResponse.ReplacementIndex],
      match.CompletionText,
      _terminalState.LastCompletionInput[
        (_currentCompletionResponse.ReplacementIndex + _currentCompletionResponse.ReplacementLength)..]);

    CommandInputText = replacementText;
    ClearCompletions();
  }

  public void ClearCompletions()
  {
    _currentCompletionResponse = null;
    _currentCompletions.Clear();
    OnPropertyChanged(nameof(HasCompletions));
  }

  public async Task GetAllCompletions(int caretIndex)
  {
    try
    {
      if (!SetCompletionContext(caretIndex))
      {
        return;
      }

      var allMatches = new List<PwshCompletionMatch>();
      var currentPage = 0;
      PwshCompletionsResponseDto? lastResponse;

      do
      {
        var requestDto = new PwshCompletionsRequestDto(
          _deviceState.CurrentDevice.Id,
          _terminalState.Id,
          _terminalState.LastCompletionInput!,
          _terminalState.LastCursorIndex,
          string.Empty,
          null,
          currentPage);

        var completionResult = await _viewerHub.Server.GetPwshCompletions(requestDto);
        if (!completionResult.IsSuccess)
        {
          _snackbar.Add(completionResult.Reason, SnackbarSeverity.Error);
          return;
        }

        if (completionResult.Value.TotalCount >= PwshCompletionsResponseDto.MaxRetrievableItems)
        {
          _snackbar.Add(
            string.Format(
              CultureInfo.CurrentCulture,
              Resources.Terminal_TooManyCompletionItems,
              completionResult.Value.TotalCount),
            SnackbarSeverity.Warning);
          return;
        }

        lastResponse = completionResult.Value;
        allMatches.AddRange(lastResponse.CompletionMatches);
        currentPage++;
      } while (lastResponse.HasMorePages);

      if (allMatches.Count == 0)
      {
        _logger.LogInformation("No completions found for input: {Input}", _terminalState.LastCompletionInput);
        ClearCompletions();
        return;
      }

      _currentCompletionResponse = new PwshCompletionsResponseDto(
        lastResponse.ReplacementIndex,
        lastResponse.ReplacementLength,
        [.. allMatches],
        false,
        allMatches.Count,
        lastResponse.CurrentPage);

      _currentCompletions.Clear();
      foreach (var match in allMatches)
      {
        _currentCompletions.Add(match);
      }

      OnPropertyChanged(nameof(HasCompletions));
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while getting all command completions.");
      _snackbar.Add(Resources.Terminal_GetCompletionsError, SnackbarSeverity.Error);
    }
  }

  public async Task GetNextCompletion(bool forward, int caretIndex)
  {
    try
    {
      if (!SetCompletionContext(caretIndex))
      {
        return;
      }

      var requestDto = new PwshCompletionsRequestDto(
        _deviceState.CurrentDevice.Id,
        _terminalState.Id,
        _terminalState.LastCompletionInput!,
        _terminalState.LastCursorIndex,
        string.Empty,
        forward);

      var completionResult = await _viewerHub.Server.GetPwshCompletions(requestDto);
      if (!completionResult.IsSuccess)
      {
        _snackbar.Add(completionResult.Reason, SnackbarSeverity.Error);
        return;
      }

      var completion = completionResult.Value;
      if (completion.CompletionMatches.Length == 0)
      {
        _logger.LogInformation("No completions found for input: {Input}", _terminalState.LastCompletionInput);
        return;
      }

      _currentCompletionResponse = completion;
      var match = completion.CompletionMatches[0];
      var replacementText = string.Concat(
        _terminalState.LastCompletionInput![..completion.ReplacementIndex],
        match.CompletionText,
        _terminalState.LastCompletionInput[(completion.ReplacementIndex + completion.ReplacementLength)..]);

      CommandInputText = replacementText;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while getting next command completion.");
      _snackbar.Add(Resources.Terminal_GetCompletionsError, SnackbarSeverity.Error);
    }
  }

  public string GetTerminalHistory(bool forward)
  {
    if (_terminalState.InputHistory.Count == 0)
    {
      return CommandInputText;
    }

    if (forward)
    {
      if (_terminalState.InputHistoryIndex < _terminalState.InputHistory.Count)
      {
        _terminalState.InputHistoryIndex++;
      }

      if (_terminalState.InputHistoryIndex >= _terminalState.InputHistory.Count)
      {
        _terminalState.InputHistoryIndex = _terminalState.InputHistory.Count;
        return _terminalState.DraftCommandInputText;
      }
    }
    else
    {
      if (_terminalState.InputHistoryIndex >= _terminalState.InputHistory.Count)
      {
        _terminalState.DraftCommandInputText = CommandInputText;
        _terminalState.InputHistoryIndex = _terminalState.InputHistory.Count - 1;
      }
      else if (_terminalState.InputHistoryIndex > 0)
      {
        _terminalState.InputHistoryIndex--;
      }
    }

    if (_terminalState.InputHistoryIndex < 0 ||
        _terminalState.InputHistoryIndex >= _terminalState.InputHistory.Count)
    {
      return CommandInputText;
    }

    return _terminalState.InputHistory[_terminalState.InputHistoryIndex];
  }

  public async Task HandleEnterKeyInput()
  {
    if (string.IsNullOrWhiteSpace(CommandInputText))
    {
      return;
    }

    try
    {
      while (_terminalState.InputHistory.Count > 500)
      {
        _terminalState.InputHistory.RemoveAt(0);
      }

      CommandInputText = CommandInputText.Trim();
      _terminalState.InputHistory.Add(CommandInputText);
      _terminalState.InputHistoryIndex = _terminalState.InputHistory.Count;
      _terminalState.DraftCommandInputText = string.Empty;

      var dto = new TerminalInputDto(_terminalState.Id, CommandInputText);
      var result = await _viewerHub.Server.SendTerminalInput(
        _deviceState.CurrentDevice.Id,
        dto);

      if (!result.IsSuccess)
      {
        _snackbar.Add(result.Reason, SnackbarSeverity.Error);
      }

      CommandInputText = string.Empty;
      _terminalState.DraftCommandInputText = string.Empty;
      _terminalState.LastCompletionInput = null;
      ClearCompletions();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while sending terminal input.");
      _snackbar.Add(Resources.Terminal_SendInputError, SnackbarSeverity.Error);
    }
  }

  public void ResetCompletionState()
  {
    _terminalState.LastCompletionInput = null;
    ClearCompletions();
  }

  public void SetCommandInputText(string text)
  {
    CommandInputText = text;
  }

  public void ShowKeyboardShortcuts()
  {
    _dialogProvider.Show<ITerminalKeyboardShortcutsDialogViewModel, TerminalKeyboardShortcutsDialogView>(
      Resources.Terminal_KeyboardShortcutsTitle,
      _terminalKeyboardShortcutsDialogViewModel);
  }

  public Task<bool> TryHandleEscape()
  {
    _terminalState.InputHistoryIndex = _terminalState.InputHistory.Count;

    if (HasCompletions)
    {
      _terminalState.LastCompletionInput = null;
      ClearCompletions();
      return Task.FromResult(true);
    }

    _terminalState.LastCompletionInput = null;
    _terminalState.DraftCommandInputText = string.Empty;

    var hadInput = !string.IsNullOrEmpty(CommandInputText);
    CommandInputText = string.Empty;

    return Task.FromResult(hadInput);
  }

  protected override async ValueTask DisposeAsync(bool disposing)
  {
    if (disposing && _viewerHub.IsConnected)
    {
      try
      {
        await _viewerHub.Server.CloseTerminalSession(_deviceState.CurrentDevice.Id, _terminalState.Id);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error while closing terminal session {TerminalId}.", _terminalState.Id);
      }
    }

    await base.DisposeAsync(disposing);
  }

  protected override async Task OnInitializeAsync()
  {
    try
    {
      await base.OnInitializeAsync();

      if (_outputLines.Count == 0 && !_terminalState.Output.IsEmpty)
      {
        foreach (var output in _terminalState.Output)
        {
          _outputLines.Add(output);
        }
      }

      var result = await _viewerHub.Server.CreateTerminalSession(
        _deviceState.CurrentDevice.Id,
        _terminalState.Id);

      if (!result.IsSuccess)
      {
        _snackbar.Add(Resources.Terminal_FailedToStart, SnackbarSeverity.Error);
        _logger.LogResult(result);
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while initializing Terminal view model.");
      _snackbar.Add(Resources.Terminal_InitializationError, SnackbarSeverity.Error);
    }
    finally
    {
      IsLoading = false;
    }
  }

  private void AppendOutput(TerminalOutputDto dto)
  {
    while (_outputLines.Count >= 500)
    {
      _outputLines.RemoveAt(0);
    }

    _outputLines.Add(dto);
  }

  private async Task HandleTerminalOutputMessage(object subscriber, DtoReceivedMessage<TerminalOutputDto> message)
  {
    var dto = message.Dto;
    if (dto.TerminalId != _terminalState.Id)
    {
      return;
    }

    while (_terminalState.Output.Count > 500)
    {
      _ = _terminalState.Output.TryDequeue(out _);
    }

    _terminalState.Output.Enqueue(dto);

    if (Dispatcher.UIThread.CheckAccess())
    {
      AppendOutput(dto);
      return;
    }

    await Dispatcher.UIThread.InvokeAsync(() => AppendOutput(dto));
  }

  private bool SetCompletionContext(int caretIndex)
  {
    if (caretIndex < 0)
    {
      _snackbar.Add(Resources.Terminal_FailedToGetCursorIndexForCompletions, SnackbarSeverity.Error);
      return false;
    }

    if (string.IsNullOrWhiteSpace(_terminalState.LastCompletionInput))
    {
      _terminalState.LastCompletionInput = CommandInputText;
      _terminalState.LastCursorIndex = caretIndex;
    }

    return true;
  }
}