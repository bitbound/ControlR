using Avalonia.Threading;
using ControlR.Assist.Services;
using CommunityToolkit.Mvvm.Input;

namespace ControlR.Assist.ViewModels;

public sealed partial class HomeViewModel : ViewModelBase, IDisposable
{
  private readonly AssistSessionController _controller;

  [ObservableProperty]
  private string? _code;

  [ObservableProperty]
  private string? _consentEmail;

  [ObservableProperty]
  private string? _consentViewerName;

  [ObservableProperty]
  private bool _isInputBlocked;

  [ObservableProperty]
  private string? _serverUrl;

  [ObservableProperty]
  private AssistSessionState _state = AssistSessionState.Disconnected;

  [ObservableProperty]
  private string? _statusMessage;

  public HomeViewModel(AssistSessionController controller, IAssistSettingsService settingsService)
  {
    _controller = controller;
    _serverUrl = settingsService.GetSettings().ServerUrl;
    _controller.StateChanged += HandleStateChanged;
    RefreshFromController();
  }

  public string ConsentPromptText =>
    string.Format(Localization.ConsentPromptFormat, ConsentViewerName ?? Localization.UnknownViewerLabel);
  public bool IsBusy => State == AssistSessionState.Connecting;
  public bool IsConnectedToServer => State != AssistSessionState.Disconnected;
  public bool IsConsentPending => State == AssistSessionState.PendingConsent;
  public bool IsSessionActive => State == AssistSessionState.Active;
  public bool ShowCode =>
    State is AssistSessionState.Ready
      or AssistSessionState.PendingConsent
      or AssistSessionState.Active;
  public bool ShowUrlEntry => State == AssistSessionState.Disconnected;

  public void Dispose() => _controller.StateChanged -= HandleStateChanged;

  private static string? FormatCode(string? code) =>
    code is { Length: 9 } formatted
      ? $"{formatted[..3]} {formatted[3..6]} {formatted[6..]}"
      : code;

  [RelayCommand]
  private Task AcceptConsentAsync() => _controller.RespondToConsentAsync(approved: true);

  [RelayCommand]
  private Task ConnectAsync() => _controller.ConnectAsync(ServerUrl ?? string.Empty, CancellationToken.None);

  [RelayCommand]
  private Task DeclineConsentAsync() => _controller.RespondToConsentAsync(approved: false);

  [RelayCommand]
  private Task DisconnectAsync() => _controller.DisconnectAsync();

  [RelayCommand]
  private Task GenerateNewCodeAsync() => _controller.GenerateNewCodeAsync();

  private void HandleStateChanged() =>
    Dispatcher.UIThread.Post(RefreshFromController, DispatcherPriority.Normal);

  private void RefreshFromController()
  {
    State = _controller.State;
    Code = FormatCode(_controller.Code);
    StatusMessage = _controller.StatusMessage;
    ConsentViewerName = _controller.PendingConsentViewerName;
    ConsentEmail = _controller.PendingConsentEmail;
    IsInputBlocked = _controller.IsInputBlocked;

    OnPropertyChanged(nameof(IsBusy));
    OnPropertyChanged(nameof(IsConsentPending));
    OnPropertyChanged(nameof(IsConnectedToServer));
    OnPropertyChanged(nameof(IsSessionActive));
    OnPropertyChanged(nameof(ShowCode));
    OnPropertyChanged(nameof(ShowUrlEntry));
    OnPropertyChanged(nameof(ConsentPromptText));
  }

  [RelayCommand]
  private Task ToggleBlockInputAsync() => _controller.ToggleBlockInputAsync();
}
