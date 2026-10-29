using Avalonia.Threading;
using ControlR.Assist.Services;
using ControlR.Libraries.Branding;
using CommunityToolkit.Mvvm.Input;

namespace ControlR.Assist.ViewModels;

public sealed partial class HomeViewModel : ViewModelBase, IDisposable
{
  private readonly AssistSessionController _controller;
  private readonly INavigationService _navigationService;

  [ObservableProperty]
  private string? _code;

  [ObservableProperty]
  private string? _consentEmail;

  [ObservableProperty]
  private string? _consentViewerName;

  [ObservableProperty]
  private bool _isViewOnly;

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

  public bool CanEditServerUrl => BrandingConstants.ControlrServerUrl is null;

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
  public bool ShowConnectAction => State == AssistSessionState.Disconnected && (UrlConfirmed || !CanEditServerUrl);

  // The server falls back to the email when the technician has no display name.
  // Showing both lines then duplicates the same text.
  public bool ShowConsentEmail =>
    !string.IsNullOrWhiteSpace(ConsentEmail)
    && !string.Equals(ConsentEmail, ConsentViewerName, StringComparison.OrdinalIgnoreCase);
  public bool ShowIdleStatus =>
    State is not (AssistSessionState.Disconnected or AssistSessionState.Connecting)
      && !string.IsNullOrWhiteSpace(StatusMessage);
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

  [RelayCommand]
  private Task ConnectAsync() =>
    _controller.ConnectAsync(
      BrandingConstants.ControlrServerUrl?.ToString() ?? ServerUrl ?? string.Empty,
      CancellationToken.None);

  [RelayCommand]
  private Task DeclineConsentAsync() => _controller.RespondToConsentAsync(approved: false);

  [RelayCommand]
  private Task DisconnectAsync() => _controller.DisconnectAsync();

  [RelayCommand]
  private Task EndSessionAsync() => _controller.EndSessionAsync();

  [RelayCommand]
  private Task GenerateNewCodeAsync() => _controller.GenerateNewCodeAsync();

  private void HandleStateChanged() =>
    Dispatcher.UIThread.Post(RefreshFromController, DispatcherPriority.Normal);

  [RelayCommand]
  private void Next()
  {
    if (!AssistSessionController.TryNormalizeServerUri(ServerUrl, out _))
    {
      StatusMessage = Localization.StatusInvalidUrl;
      return;
    }

    StatusMessage = null;
    UrlConfirmed = true;
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

    OnPropertyChanged(nameof(IsBusy));
    OnPropertyChanged(nameof(IsConsentPending));
    OnPropertyChanged(nameof(IsConnectedToServer));
    OnPropertyChanged(nameof(IsSessionActive));
    OnPropertyChanged(nameof(ShowCode));
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
