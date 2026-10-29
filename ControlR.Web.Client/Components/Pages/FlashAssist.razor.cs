using Microsoft.AspNetCore.Components.Web;

namespace ControlR.Web.Client.Components.Pages;

// ReSharper disable once ClassNeverInstantiated.Global
public partial class FlashAssist : ComponentBase, IAsyncDisposable
{
  private readonly uint _commandTimeoutSeconds = 30;
  private readonly CancellationTokenSource _pageCts = new();

  private string? _accessCode;
  private bool _isJoining;
  private Guid? _linkedSessionId;
  private IDisposable? _messengerRegistration;
  private string? _remoteDisplayName;
  private bool _sessionEndedHandled;
  private AssistPageState _state = AssistPageState.CodeEntry;
  private string? _statusMessage;
  private Severity _statusSeverity = Severity.Info;

  [Inject]
  public required IHubConnection<IAssistHub> AssistHub { get; init; }

  [Inject]
  public required IHubConnector HubConnector { get; init; }

  [Inject]
  public required ILogger<FlashAssist> Logger { get; init; }

  [Inject]
  public required IMessenger Messenger { get; init; }

  [Inject]
  public required NavigationManager NavManager { get; init; }

  [Inject]
  public required IRemoteControlState RemoteControlState { get; init; }

  [Inject]
  public required IViewerRemoteControlStream RemoteControlStream { get; init; }

  [Inject]
  public required IScreenWake ScreenWake { get; init; }

  [Inject]
  public required ISnackbar Snackbar { get; init; }

  [Inject]
  public required IUserPreferencesProvider UserPreferences { get; init; }

  private AssistPageState CurrentState => _state;

  public async ValueTask DisposeAsync()
  {
    _messengerRegistration?.Dispose();
    _pageCts.Cancel();
    await CleanupSession(sendCloseStreamingSession: false, endHubSession: false);
    _pageCts.Dispose();
  }

  protected override Task OnInitializedAsync()
  {
    _messengerRegistration = Messenger.Register<DtoReceivedMessage<AssistConsentRespondedDto>>(
      this,
      HandleAssistConsentResponded);

    Messenger.Register<DtoReceivedMessage<AssistSessionEndedDto>>(
      this,
      HandleAssistSessionEnded);

    return base.OnInitializedAsync();
  }

  private async Task CleanupSession(bool sendCloseStreamingSession, bool endHubSession)
  {
    RemoteControlState.ConnectionClosedRegistration?.Dispose();
    RemoteControlState.ConnectionClosedRegistration = null;

    if (RemoteControlState.CurrentSession is { } session)
    {
      if (sendCloseStreamingSession)
      {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await RemoteControlStream.SendCloseStreamingSession(cts.Token);
      }

      if (endHubSession)
      {
        try
        {
          using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
          _ = await AssistHub.Server.EndAssistSession(new(session.SessionId));
        }
        catch (Exception ex)
        {
          Logger.LogWarning(ex, "Failed to end assist session {SessionId} on the server.", session.SessionId);
        }
      }

      RemoteControlState.CurrentSession = null;
    }

    await RemoteControlStream.Close();
    await ScreenWake.SetScreenWakeLock(false);
  }

  private async Task HandleAssistConsentResponded(
    object subscriber,
    DtoReceivedMessage<AssistConsentRespondedDto> message)
  {
    var dto = message.Dto;
    if (_linkedSessionId is not Guid linkedSessionId || dto.SessionId != linkedSessionId)
    {
      return;
    }

    if (!dto.Approved || string.IsNullOrWhiteSpace(dto.AccessToken))
    {
      await InvokeAsync(() =>
      {
        _state = AssistPageState.CodeEntry;
        _statusMessage = "The remote user declined the connection.";
        _statusSeverity = Severity.Warning;
        StateHasChanged();
      });
      return;
    }

    await StartRemoteControlAsync(dto.SessionId, dto.AccessToken);
  }

  private async Task HandleAssistSessionEnded(object subscriber, DtoReceivedMessage<AssistSessionEndedDto> message)
  {
    var dto = message.Dto;
    if (_linkedSessionId is not Guid linkedSessionId || dto.SessionId != linkedSessionId)
    {
      return;
    }

    // The relay also reports a closed connection when the session is torn down. Whichever
    // arrives first processes; the hub-provided reason wins over the generic disconnect message.
    if (_sessionEndedHandled)
    {
      return;
    }

    _sessionEndedHandled = true;

    await CleanupSession(sendCloseStreamingSession: false, endHubSession: false);

    await InvokeAsync(() =>
    {
      _state = AssistPageState.CodeEntry;
      _statusMessage = dto.Reason;
      _statusSeverity = Severity.Info;
      StateHasChanged();
    });
  }

  private async Task HandleCancelClicked()
  {
    await CleanupSession(sendCloseStreamingSession: false, endHubSession: true);
    _state = AssistPageState.CodeEntry;
    _statusMessage = "Connection request cancelled.";
    _statusSeverity = Severity.Info;
  }

  private async Task HandleCodeKeyDown(KeyboardEventArgs args)
  {
    if (args.Key == "Enter")
    {
      await HandleConnectClicked();
    }
  }

  private async Task HandleConnectClicked()
  {
    if (_isJoining)
    {
      return;
    }

    var code = new string((_accessCode ?? string.Empty).Where(char.IsDigit).ToArray());
    if (code.Length != 9)
    {
      _statusMessage = "Enter the 9-digit access code shown on the customer's screen.";
      _statusSeverity = Severity.Warning;
      return;
    }

    _isJoining = true;
    _sessionEndedHandled = false;
    _statusMessage = null;

    try
    {
      await HubConnector.Connect<IAssistHub>(AppConstants.AssistHubPath, _pageCts.Token);

      var result = await AssistHub.Server.JoinAssistSession(new(code));
      if (!result.IsSuccess || result.Value is null)
      {
        _statusMessage = result.Reason ?? "The code is invalid or no longer active.";
        _statusSeverity = Severity.Error;
        return;
      }

      _linkedSessionId = result.Value.SessionId;
      _remoteDisplayName = "the remote user";
      _state = AssistPageState.WaitingForConsent;
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while joining assist session.");
      _statusMessage = "An error occurred while connecting.";
      _statusSeverity = Severity.Error;
    }
    finally
    {
      _isJoining = false;
      await InvokeAsync(StateHasChanged);
    }
  }

  private async Task HandleConnectionLost()
  {
    await CleanupSession(sendCloseStreamingSession: false, endHubSession: true);

    if (_sessionEndedHandled)
    {
      return;
    }

    await InvokeAsync(() =>
    {
      _state = AssistPageState.CodeEntry;
      _statusMessage = "The connection to the remote device was lost.";
      _statusSeverity = Severity.Warning;
      StateHasChanged();
    });
  }

  private async Task HandleDisconnectRequested()
  {
    await CleanupSession(sendCloseStreamingSession: true, endHubSession: true);

    await InvokeAsync(() =>
    {
      _state = AssistPageState.CodeEntry;
      _statusMessage = "Remote control session disconnected.";
      _statusSeverity = Severity.Info;
      StateHasChanged();
    });
  }

  private async Task HandleFatalStreamingError(string message)
  {
    await CleanupSession(sendCloseStreamingSession: false, endHubSession: true);

    await InvokeAsync(() =>
    {
      _state = AssistPageState.CodeEntry;
      _statusMessage = message;
      _statusSeverity = Severity.Error;
      StateHasChanged();
    });
  }

  private async Task InitializeCaptureSettings()
  {
    var preferences = await UserPreferences.GetPreferences();
    RemoteControlState.CaptureCursor = preferences.CaptureCursor;
    RemoteControlState.EnableDirectX = preferences.EnableDirectX;
    RemoteControlState.EncodingFormat = preferences.EncodingFormat;
    RemoteControlState.IsAutoQualityEnabled = preferences.IsAutoQualityEnabled;
    RemoteControlState.ManualQuality = preferences.ManualQuality;
    RemoteControlState.AutoQualityLowerThresholdMbps = preferences.AutoQualityLowerThresholdMbps;
    RemoteControlState.AutoQualityMaximum = preferences.AutoQualityMaximum;
    RemoteControlState.AutoQualityMinimum = preferences.AutoQualityMinimum;
    RemoteControlState.AutoQualityUpperThresholdMbps = preferences.AutoQualityUpperThresholdMbps;
    RemoteControlState.IsMaxBandwidthEnabled = preferences.IsMaxBandwidthEnabled;
    RemoteControlState.MaxBandwidthMbps = preferences.MaxBandwidthMbps;
  }

  private async Task SendCaptureSettings()
  {
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
    await RemoteControlStream.SendCaptureSettings(
      new UpdateCaptureSettingsDto(
        RemoteControlState.CaptureCursor,
        RemoteControlState.EnableDirectX,
        RemoteControlState.IsAutoQualityEnabled,
        RemoteControlState.ManualQuality,
        RemoteControlState.AutoQualityLowerThresholdMbps,
        RemoteControlState.AutoQualityMaximum,
        RemoteControlState.AutoQualityMinimum,
        RemoteControlState.AutoQualityUpperThresholdMbps,
        RemoteControlState.IsMaxBandwidthEnabled,
        RemoteControlState.MaxBandwidthMbps,
        RemoteControlState.EncodingFormat),
      cts.Token);
  }

  private async Task StartRemoteControlAsync(Guid sessionId, string accessToken)
  {
    try
    {
      _state = AssistPageState.Connecting;
      await InvokeAsync(StateHasChanged);

      RemoteControlState.IsBlockUserInputEnabled = false;
      RemoteControlState.IsPrivacyScreenEnabled = false;
      await InitializeCaptureSettings();

      var session = RemoteControlSession.CreateForAssistSession(sessionId);
      var serverUri = new Uri(NavManager.BaseUri).ToWebsocketUri();
      var relayUri = RelayUriBuilder.Build(
        serverUri,
        AppConstants.WebSocketRelayPath,
        session.SessionId,
        accessToken,
        RelayRole.Requester,
        _commandTimeoutSeconds);

      using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_commandTimeoutSeconds));
      RemoteControlState.ConnectionClosedRegistration?.Dispose();
      await RemoteControlStream.Connect(session, relayUri, cts.Token);
      RemoteControlState.ConnectionClosedRegistration = RemoteControlStream.OnClosed(HandleConnectionLost);
      RemoteControlState.CurrentSession = session;
      await SendCaptureSettings();

      await ScreenWake.SetScreenWakeLock(true);

      _state = AssistPageState.Connected;
      _statusMessage = null;
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while starting assist remote control session.");
      await CleanupSession(sendCloseStreamingSession: false, endHubSession: true);

      _state = AssistPageState.CodeEntry;
      _statusMessage = "An error occurred while connecting to the remote device.";
      _statusSeverity = Severity.Error;
    }
    finally
    {
      await InvokeAsync(StateHasChanged);
    }
  }

  private enum AssistPageState
  {
    CodeEntry,
    Connecting,
    WaitingForConsent,
    Connected
  }
}
