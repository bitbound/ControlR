using Avalonia.Threading;
using ControlR.Assist.Services;
using ControlR.Libraries.Branding;
using CommunityToolkit.Mvvm.Input;

namespace ControlR.Assist.ViewModels;

public sealed partial class HomeViewModel : ViewModelBase, IDisposable
{
  private const int NewCodeCooldownSeconds = 15;

  private readonly AssistSessionController _controller;
  private readonly INavigationService _navigationService;

  private string? _activeViewerEmail;
  private string? _activeViewerName;

  [ObservableProperty]
  private string? _code;

  [ObservableProperty]
  private string? _consentEmail;

  [ObservableProperty]
  private string? _consentViewerName;

  [ObservableProperty]
  [NotifyPropertyChangedFor(nameof(NewCodeText))]
  private int _cooldownSeconds;

  [ObservableProperty]
  [NotifyPropertyChangedFor(nameof(NewCodeText))]
  private bool _isNewCodeEnabled = true;

  [ObservableProperty]
  private bool _isViewOnly;

  private string? _pendingViewerEmail;
  private string? _pendingViewerName;

  [ObservableProperty]
  private string? _serverUrl;

  [ObservableProperty]
  private AssistSessionState _state = AssistSessionState.Disconnected;

  [ObservableProperty]
  private string? _statusMessage;

  [ObservableProperty]
  private bool _urlConfirmed;

  public HomeViewModel(AssistSessionController controller, IAssistSettingsService settingsService, INavigationService navigationService)
  {
    _controller = controller;
    _navigationService = navigationService;
    _serverUrl = BrandingConstants.ControlrServerUrl?.ToString() ?? settingsService.GetSettings().ServerUrl;
    _urlConfirmed = BrandingConstants.ControlrServerUrl is not null;
    _controller.StateChanged += HandleStateChanged;
    RefreshFromController();
  }

  public string? ActiveViewerEmail => _activeViewerEmail;
  public string? ActiveViewerName => _activeViewerName;
  public bool CanEditServerUrl => BrandingConstants.ControlrServerUrl is null;

  public string ConsentPromptText =>
    string.Format(Localization.ConsentPromptFormat, ConsentViewerName ?? Localization.UnknownViewerLabel);
  public bool IsBusy => State == AssistSessionState.Connecting;
  public bool IsConnectedToServer => State != AssistSessionState.Disconnected;
  public bool IsConsentPending => State == AssistSessionState.PendingConsent;
  public bool IsSessionActive => State == AssistSessionState.Active;

  public string NewCodeText =>
    IsNewCodeEnabled
      ? Localization.NewCodeButtonText
      : string.Format(Localization.NewCodeCooldownFormat, CooldownSeconds);

  /// <summary>
  /// Set by the main window to present a modal confirmation. Receives the message and returns
  /// true when the user confirms.
  /// </summary>
  public Func<string, Task<bool>>? RequestConfirmationAsync { get; set; }

  public bool ShowCode =>
    State is AssistSessionState.Ready
      or AssistSessionState.PendingConsent
      or AssistSessionState.Active;
  public bool ShowConnectAction => State == AssistSessionState.Disconnected && (UrlConfirmed || !CanEditServerUrl);

  // The decorated status line replaces the plain one while an active session has a viewer email.
  public bool ShowConnectedStatus =>
    IsSessionActive
    && !string.IsNullOrWhiteSpace(ActiveViewerEmail)
    && !string.Equals(ActiveViewerEmail, ActiveViewerName, StringComparison.OrdinalIgnoreCase);

  // The server falls back to the email when the technician has no display name.
  // Showing both then duplicates the same text.
  public bool ShowConsentEmail =>
    !string.IsNullOrWhiteSpace(ConsentEmail)
    && !string.Equals(ConsentEmail, ConsentViewerName, StringComparison.OrdinalIgnoreCase);
  public bool ShowIdleStatus =>
    State is not (AssistSessionState.Disconnected or AssistSessionState.Connecting)
      && !string.IsNullOrWhiteSpace(StatusMessage)
      && !ShowConnectedStatus;
  public bool ShowStatusAlert => State == AssistSessionState.Disconnected && !string.IsNullOrWhiteSpace(StatusMessage);
  public bool ShowStepCard => ShowUrlEntry || ShowConnectAction || IsBusy;
  public bool ShowTrustNotice => State == AssistSessionState.Disconnected;
  public bool ShowUrlEntry => ShowTrustNotice && CanEditServerUrl && !UrlConfirmed;

  public void Dispose() => _controller.StateChanged -= HandleStateChanged;

  private static string? FormatCode(string? code) =>
    code is { Length: 9 } formatted
      ? $"{formatted[..3]} {formatted[3..6]} {formatted[6..]}"
      : code;

  [RelayCommand]
  private Task AcceptConsentAsync() => _controller.RespondToConsentAsync(approved: true);

  [RelayCommand]
  private void ChangeServer()
  {
    UrlConfirmed = false;
  }

  private async Task<bool> ConfirmAsync(string message) =>
    RequestConfirmationAsync is null || await RequestConfirmationAsync.Invoke(message);

  [RelayCommand]
  private Task ConnectAsync() =>
    _controller.ConnectAsync(
      BrandingConstants.ControlrServerUrl?.ToString() ?? ServerUrl ?? string.Empty,
      CancellationToken.None);

  [RelayCommand]
  private Task DeclineConsentAsync() => _controller.RespondToConsentAsync(approved: false);

  [RelayCommand]
  private async Task DisconnectAsync()
  {
    if (!await ConfirmAsync(Localization.ConfirmDisconnectMessage))
    {
      return;
    }

    await _controller.DisconnectAsync();
  }

  [RelayCommand]
  private async Task EndSessionAsync()
  {
    if (!await ConfirmAsync(Localization.ConfirmEndSessionMessage))
    {
      return;
    }

    await _controller.EndSessionAsync();
  }

  [RelayCommand(CanExecute = nameof(IsNewCodeEnabled))]
  private async Task GenerateNewCodeAsync()
  {
    IsNewCodeEnabled = false;
    CooldownSeconds = NewCodeCooldownSeconds;

    try
    {
      await _controller.GenerateNewCodeAsync();

      for (var remaining = NewCodeCooldownSeconds; remaining > 0; remaining--)
      {
        CooldownSeconds = remaining;
        await Task.Delay(TimeSpan.FromSeconds(1));
      }
    }
    finally
    {
      CooldownSeconds = 0;
      IsNewCodeEnabled = true;
    }
  }

  private void HandleStateChanged() =>
    Dispatcher.UIThread.Post(RefreshFromController, DispatcherPriority.Normal);

  [RelayCommand]
  private void Next()
  {
    if (!AssistSessionController.TryNormalizeServerUri(ServerUrl, out var normalized))
    {
      StatusMessage = Localization.StatusInvalidUrl;
      return;
    }

    // Show and persist the scheme://host[:port] form the connect will actually use.
    ServerUrl = normalized.ToString();
    StatusMessage = null;
    UrlConfirmed = true;
  }

  partial void OnStatusMessageChanged(string? value)
  {
    OnPropertyChanged(nameof(ShowStatusAlert));
    OnPropertyChanged(nameof(ShowIdleStatus));
  }

  partial void OnUrlConfirmedChanged(bool value) => RaiseFlowPropertyChanges();

  private void RaiseFlowPropertyChanges()
  {
    OnPropertyChanged(nameof(ShowUrlEntry));
    OnPropertyChanged(nameof(ShowConnectAction));
    OnPropertyChanged(nameof(ShowStepCard));
  }

  private void RefreshFromController()
  {
    State = _controller.State;
    Code = FormatCode(_controller.Code);
    StatusMessage = _controller.StatusMessage;
    ConsentViewerName = _controller.PendingConsentViewerName;
    ConsentEmail = _controller.PendingConsentEmail;
    IsViewOnly = _controller.IsViewOnly;

    // The controller clears the pending consent fields when the session starts, so cache the
    // viewer identity while consent is pending and reuse it for the active-session status line.
    if (State == AssistSessionState.PendingConsent)
    {
      _pendingViewerName = ConsentViewerName;
      _pendingViewerEmail = ConsentEmail;
    }
    else if (State != AssistSessionState.Active)
    {
      _pendingViewerName = null;
      _pendingViewerEmail = null;
    }

    _activeViewerName = State == AssistSessionState.Active ? _pendingViewerName : null;
    _activeViewerEmail = State == AssistSessionState.Active ? _pendingViewerEmail : null;

    OnPropertyChanged(nameof(IsBusy));
    OnPropertyChanged(nameof(IsConsentPending));
    OnPropertyChanged(nameof(IsConnectedToServer));
    OnPropertyChanged(nameof(IsSessionActive));
    OnPropertyChanged(nameof(ShowCode));
    OnPropertyChanged(nameof(ShowConnectedStatus));
    OnPropertyChanged(nameof(ShowIdleStatus));
    OnPropertyChanged(nameof(ShowStatusAlert));
    OnPropertyChanged(nameof(ShowStepCard));
    OnPropertyChanged(nameof(ShowTrustNotice));
    RaiseFlowPropertyChanges();
    OnPropertyChanged(nameof(ConsentPromptText));
    OnPropertyChanged(nameof(ShowConsentEmail));
  }

  [RelayCommand]
  private void SetViewOnly() => _controller.SetViewOnly(IsViewOnly);

  [RelayCommand]
  private void ShowAbout() => _navigationService.NavigateTo<AboutViewModel>();
}
