using System.Collections.Specialized;
using System.Globalization;
using System.Net.WebSockets;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using ControlR.Libraries.Avalonia.Controls.Dialogs;
using ControlR.Libraries.Avalonia.Controls.Snackbar;
using ControlR.Libraries.Api.Contracts.Enums;
using ControlR.Viewer.Avalonia.ViewModels.Dialogs;
using ControlR.Viewer.Avalonia.Views.Dialogs;
using ControlR.Libraries.Messenger.Extensions;

namespace ControlR.Viewer.Avalonia.ViewModels;

public interface IRemoteControlViewModel : IViewModelBase
{
  string? AlertMessage { get; set; }
  SnackbarSeverity AlertSeverity { get; }
  SignalingState CurrentState { get; }
  ObservableCollection<DesktopSessionViewModel> DesktopSessions { get; }
  string DesktopSessionTitle { get; }
  bool HasDesktopSessions { get; }
  bool IsReconnecting { get; set; }
  bool IsRemoteDisplayVisible { get; }
  bool IsViewOnlyEnabled { get; set; }
  string? LoadingMessage { get; set; }
  IAsyncRelayCommand RefreshSessionsCommand { get; }
  IRemoteDisplayViewModel RemoteDisplayViewModel { get; }
}

public partial class RemoteControlViewModel : ViewModelBase<RemoteControlView>, IRemoteControlViewModel
{
  private const uint CommandTimeoutSeconds = 30;

  private readonly IDeviceState _deviceState;
  private readonly IDialogProvider _dialogProvider;
  private readonly IHubConnection<IViewerHub> _hubConnection;
  private readonly ILogger<RemoteControlViewModel> _logger;
  private readonly IDesktopPreviewDialogViewModelFactory _previewFactory;
  private readonly IRemoteControlState _remoteControlState;
  private readonly IViewerRemoteControlStream _remoteControlStream;
  private readonly ISnackbar _snackbar;
  private readonly TimeProvider _timeProvider;
  private readonly IOptions<ControlrViewerOptions> _viewerOptions;

  [ObservableProperty]
  [NotifyPropertyChangedFor(nameof(CurrentState))]
  [NotifyPropertyChangedFor(nameof(IsRemoteDisplayVisible))]
  private string? _alertMessage;
  [ObservableProperty]
  [NotifyPropertyChangedFor(nameof(CurrentState))]
  [NotifyPropertyChangedFor(nameof(IsRemoteDisplayVisible))]
  private SnackbarSeverity _alertSeverity = SnackbarSeverity.Info;
  [ObservableProperty]
  [NotifyPropertyChangedFor(nameof(CurrentState))]
  [NotifyPropertyChangedFor(nameof(IsRemoteDisplayVisible))]
  private bool _isReconnecting;
  [ObservableProperty]
  [NotifyPropertyChangedFor(nameof(CurrentState))]
  [NotifyPropertyChangedFor(nameof(IsRemoteDisplayVisible))]
  private string? _loadingMessage = Resources.ConnectionStatus_Connecting;

  public RemoteControlViewModel(
    TimeProvider timeProvider,
    IDeviceState deviceState,
    IViewerRemoteControlStream streamingClient,
    IRemoteControlState remoteControlState,
    IHubConnection<IViewerHub> hubConnection,
    IDialogProvider dialogProvider,
    IDesktopPreviewDialogViewModelFactory desktopPreviewDialogViewModelFactory,
    ISnackbar snackbar,
    IMessenger messenger,
    IOptions<ControlrViewerOptions> viewerOptions,
    ILogger<RemoteControlViewModel> logger,
    IRemoteDisplayViewModel remoteDisplayViewModel)
  {
    _deviceState = deviceState;
    _dialogProvider = dialogProvider;
    _hubConnection = hubConnection;
    _logger = logger;
    _previewFactory = desktopPreviewDialogViewModelFactory;
    _remoteControlState = remoteControlState;
    _remoteControlStream = streamingClient;
    _snackbar = snackbar;
    _timeProvider = timeProvider;
    _viewerOptions = viewerOptions;
    RemoteDisplayViewModel = remoteDisplayViewModel;

    messenger.RegisterEvent(this, EventKinds.RemoteControlDisconnectRequested, HandleDisconnectRequested);
  }

  public SignalingState CurrentState
  {
    get
    {
      if (IsReconnecting)
      {
        return SignalingState.Reconnecting;
      }

      if (_remoteControlStream.State == WebSocketState.Open)
      {
        return SignalingState.ConnectionActive;
      }

      if (!string.IsNullOrWhiteSpace(LoadingMessage))
      {
        return SignalingState.Loading;
      }

      if (!string.IsNullOrWhiteSpace(AlertMessage))
      {
        return SignalingState.Alert;
      }

      if (!_deviceState.IsDeviceLoaded)
      {
        return SignalingState.Unknown;
      }

      if (_deviceState.CurrentDevice.Platform
          is not SystemPlatform.Windows
          and not SystemPlatform.MacOs
          and not SystemPlatform.Linux)
      {
        return SignalingState.UnsupportedOperatingSystem;
      }

      return SignalingState.SessionSelect;
    }
  }
  public ObservableCollection<DesktopSessionViewModel> DesktopSessions { get; } = [];
  public string DesktopSessionTitle
  {
    get
    {
      var deviceName = _deviceState.TryGetCurrentDevice()?.Name;
      if (string.IsNullOrWhiteSpace(deviceName))
      {
        deviceName = Resources.ConnectionStatus_Unknown;
      }

      return string.Format(
        CultureInfo.CurrentCulture,
        Resources.RemoteControl_DesktopSessionOnDevice,
        deviceName);
    }
  }
  public bool HasDesktopSessions => DesktopSessions.Count > 0;
  public bool IsRemoteDisplayVisible => CurrentState == SignalingState.ConnectionActive;
  public bool IsViewOnlyEnabled
  {
    get => _remoteControlState.IsViewOnlyEnabled;
    set
    {
      if (_remoteControlState.IsViewOnlyEnabled == value)
      {
        return;
      }

      _remoteControlState.IsViewOnlyEnabled = value;
      OnPropertyChanged();
    }
  }
  public IRemoteDisplayViewModel RemoteDisplayViewModel { get; }

  protected override void Dispose(bool disposing)
  {
    _dialogProvider.Close();
    _remoteControlState.ConnectionClosedRegistration?.Dispose();
    DesktopSessions.CollectionChanged -= HandleDesktopSessionsChanged;
    foreach (var session in DesktopSessions)
    {
      session.PreviewRequested -= HandlePreviewRequested;
      session.ConnectRequested -= HandleConnectRequested;
    }
    base.Dispose(disposing);
  }

  protected override async Task OnInitializeAsync()
  {
    try
    {
      await base.OnInitializeAsync();

      LoadingMessage = Resources.Loading;
      AlertMessage = null;
      AlertSeverity = SnackbarSeverity.Info;

      DesktopSessions.CollectionChanged -= HandleDesktopSessionsChanged;
      DesktopSessions.CollectionChanged += HandleDesktopSessionsChanged;
      OnPropertyChanged(nameof(DesktopSessionTitle));

      if (CurrentState == SignalingState.ConnectionActive)
      {
        return;
      }

      await GetDeviceDesktopSessions(quiet: true);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to retrieve desktop sessions on remote device.");
      AlertMessage = Resources.RemoteControl_FailedToRetrieveSessions;
      AlertSeverity = SnackbarSeverity.Warning;
    }
    finally
    {
      LoadingMessage = null;
    }
  }

  private async Task GetDeviceDesktopSessions(bool quiet)
  {
    try
    {
      var desktopSessions = await _hubConnection.Server.GetActiveDesktopSessions(_viewerOptions.Value.DeviceId);

      foreach (var existingSession in DesktopSessions)
      {
        existingSession.PreviewRequested -= HandlePreviewRequested;
        existingSession.ConnectRequested -= HandleConnectRequested;
      }

      DesktopSessions.Clear();
      foreach (var session in desktopSessions)
      {
        var vm = new DesktopSessionViewModel(session);
        vm.PreviewRequested += HandlePreviewRequested;
        vm.ConnectRequested += HandleConnectRequested;
        DesktopSessions.Add(vm);
      }

      if (!quiet)
      {
        _snackbar.Add(Resources.RemoteControl_SessionsRefreshed, SnackbarSeverity.Info);
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to get active sessions.");
      if (!quiet)
      {
        AlertMessage = Resources.RemoteControl_FailedToGetSessions;
        AlertSeverity = SnackbarSeverity.Warning;
      }
    }
  }

  private async void HandleConnectRequested(object? sender, DesktopSession session)
  {
    await StartRemoteControl(session, false);
  }

  private void HandleDesktopSessionsChanged(object? sender, NotifyCollectionChangedEventArgs e)
  {
    OnPropertyChanged(nameof(CurrentState));
    OnPropertyChanged(nameof(IsRemoteDisplayVisible));
    OnPropertyChanged(nameof(HasDesktopSessions));
  }

  private async Task HandleDisconnectRequested()
  {
    try
    {
      _dialogProvider.Close();
      _remoteControlState.ConnectionClosedRegistration?.Dispose();
      DesktopSessions.Clear();
      _remoteControlState.CurrentSession = null;
      LoadingMessage = Resources.RemoteControl_RefreshingSessions;
      using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
      await _remoteControlStream.SendCloseStreamingSession(cts.Token);
      var closeResult = await _remoteControlStream.Close();
      if (!closeResult.IsSuccess)
      {
        _snackbar.Add(Resources.RemoteControl_ErrorDisconnecting, SnackbarSeverity.Warning);
      }
      OnPropertyChanged(nameof(CurrentState));
      OnPropertyChanged(nameof(IsRemoteDisplayVisible));
      await GetDeviceDesktopSessions(quiet: true);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while disconnecting from remote control session.");
      AlertMessage = Resources.RemoteControl_ErrorDisconnecting;
      AlertSeverity = SnackbarSeverity.Warning;
    }
    finally
    {
      LoadingMessage = null;
    }
  }

  private async void HandlePreviewRequested(object? sender, DesktopSession session)
  {
    await ShowPreviewDialog(session);
  }

  private async Task HandleStreamingConnectionLost()
  {
    if (await Reconnect())
    {
      return;
    }

    _remoteControlState.CurrentSession = null;
    await GetDeviceDesktopSessions(quiet: true);
  }

  private async Task<bool> Reconnect()
  {
    try
    {
      IsReconnecting = true;
      AlertMessage = null;
      LoadingMessage = null;

      for (var i = 0; i < 5; i++)
      {
        try
        {
          if (i > 0)
          {
            await Task.Delay(TimeSpan.FromSeconds(3), _timeProvider);
          }

          await GetDeviceDesktopSessions(quiet: true);
          if (DesktopSessions.Count == 0)
          {
            continue;
          }

          // If multiple sessions exist, let the user choose manually
          if (DesktopSessions.Count > 1)
          {
            break;
          }

          // Single session - attempt automatic reconnection
          if (await StartRemoteControl(DesktopSessions[0].Session, true))
          {
            return true;
          }
        }
        catch (Exception ex)
        {
          _logger.LogError(ex, "Error while reconnecting.");
        }
      }

      AlertMessage = Resources.RemoteControl_FailedToReconnect;
      AlertSeverity = SnackbarSeverity.Warning;
      return false;
    }
    finally
    {
      IsReconnecting = false;
    }
  }

  [RelayCommand]
  private async Task RefreshSessions()
  {
    AlertMessage = null;
    AlertSeverity = SnackbarSeverity.Info;
    await GetDeviceDesktopSessions(quiet: false);
  }

  private async Task ShowPreviewDialog(DesktopSession session)
  {
    var previewDialogViewModel = _previewFactory.Create(session);
    
    _dialogProvider.Show<IDesktopPreviewDialogViewModel, DesktopPreviewDialogView>(
      Resources.RemoteControl_DesktopPreview,
      previewDialogViewModel);

    await previewDialogViewModel.LoadPreview(showSuccessSnackbar: false);
  }

  private async Task<bool> StartRemoteControl(DesktopSession desktopSession, bool quiet)
  {
    try
    {
      if (!quiet)
      {
        LoadingMessage = Resources.RemoteControl_StartingSession;
      }

      _remoteControlState.IsBlockUserInputEnabled = false;
      _remoteControlState.IsPrivacyScreenEnabled = false;

      DesktopSessions.Clear();

      var currentDevice = _deviceState.TryGetCurrentDevice();
      if (currentDevice is null)
      {
        AlertMessage = Resources.RemoteControl_DeviceInfoUnavailable;
        AlertSeverity = SnackbarSeverity.Warning;
        LoadingMessage = null;
        return false;
      }

      var session = new RemoteControlSession(
        currentDevice,
        desktopSession.SystemSessionId,
        desktopSession.ProcessId,
        desktopSession.Type);

      var accessToken = RandomGenerator.CreateAccessToken();

      var serverUri = _viewerOptions.Value.BaseUrl.ToWebsocketUri();
      var desktopRelayUri = RelayUriBuilder.Build(
        serverUri,
        AppConstants.WebSocketRelayPath,
        session.SessionId,
        accessToken,
        RelayRole.Responder,
        CommandTimeoutSeconds);

      var requestDto = new RemoteControlSessionRequestDto(
        session.SessionId,
        desktopRelayUri,
        session.TargetSystemSession,
        session.TargetProcessId,
        session.Device.Id,
        NotifyUserOnSessionStart: false,
        RequireConsent: false);

      var remoteControlSessionResult = await _hubConnection.Server.RequestRemoteControlSession(session.Device.Id, requestDto);

      if (!remoteControlSessionResult.IsSuccess)
      {
        AlertMessage = remoteControlSessionResult.Reason;
        AlertSeverity = SnackbarSeverity.Warning;
        return false;
      }

      var viewerRelayUri = RelayUriBuilder.Build(
        serverUri,
        AppConstants.WebSocketRelayPath,
        session.SessionId,
        accessToken,
        RelayRole.Requester,
        CommandTimeoutSeconds);

      using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(CommandTimeoutSeconds));

      await _remoteControlStream.Connect(
        viewerRelayUri,
        wsOptions =>
        {
          wsOptions.SetRequestHeader(ControlrViewerOptions.PersonalAccessTokenHeaderName, _viewerOptions.Value.PersonalAccessToken);
        },
        cts.Token);


      _remoteControlState.ConnectionClosedRegistration?.Dispose();
      _remoteControlState.ConnectionClosedRegistration = _remoteControlStream.OnClosed(async () =>
      {
        await Dispatcher.UIThread.InvokeAsync(HandleStreamingConnectionLost);
      });

      _remoteControlState.CurrentSession = session;
      OnPropertyChanged(nameof(CurrentState));
      OnPropertyChanged(nameof(IsRemoteDisplayVisible));

      return true;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while requesting remote control session.");
      if (!quiet)
      {
        AlertMessage = Resources.RemoteControl_ErrorRequestingSession;
        AlertSeverity = SnackbarSeverity.Error;
      }

      _remoteControlState.CurrentSession = null;
      return false;
    }
    finally
    {
      LoadingMessage = null;
    }
  }
}

