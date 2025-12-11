using System.Net.WebSockets;
using ControlR.Libraries.Shared.Dtos.StreamerDtos;
using ControlR.Web.Client.StateManagement.DeviceAccess;
using Microsoft.AspNetCore.Components;

namespace ControlR.Web.Client.Components.Pages.DeviceAccess;

// ReSharper disable once ClassNeverInstantiated.Global
public partial class RemoteControl : ViewportAwareComponent
{
  private readonly int _commandTimeoutSeconds = 30;
  private string? _alertMessage;
  private Severity _alertSeverity;
  private bool _isReconnecting;
  private string? _loadingMessage = "Connecting";
  private DesktopSession[]? _systemSessions;
  
  [Inject]
  public required IDeviceState DeviceAccessState { get; init; }

  [SupplyParameterFromQuery]
  public required Guid DeviceId { get; init; }

  [Inject]
  public required IDialogService DialogService { get; init; }

  [Inject]
  public required ILogger<RemoteControl> Logger { get; init; }

  [Inject]
  public required NavigationManager NavManager { get; init; }

  [Inject]
  public required IRemoteControlState RemoteControlState { get; init; }

  [Inject]
  public required IScreenWake ScreenWake { get; init; }

  [Inject]
  public required ISnackbar Snackbar { get; init; }

  [Inject]
  public required IViewerStreamingClient StreamingClient { get; init; }

  [Inject]
  public required IUserSettingsProvider UserSettings { get; init; }


  [Inject]
  public required IHubConnection<IViewerHub> ViewerHub { get; init; }


  private string AlertIcon =>
    _alertSeverity switch
    {
      Severity.Normal or Severity.Info => Icons.Material.Outlined.Info,
      Severity.Success => Icons.Material.Outlined.CheckCircleOutline,
      Severity.Warning => Icons.Material.Outlined.Warning,
      Severity.Error => Icons.Material.Outlined.Error,
      _ => Icons.Material.Outlined.Info
    };

  private SignalingState CurrentState
  {
    get
    {
      if (_isReconnecting)
      {
        return SignalingState.Reconnecting;
      }

      if (StreamingClient.State == WebSocketState.Open)
      {
        return SignalingState.ConnectionActive;
      }

      if (!string.IsNullOrWhiteSpace(_loadingMessage))
      {
        return SignalingState.Loading;
      }

      if (!string.IsNullOrWhiteSpace(_alertMessage))
      {
        return SignalingState.Alert;
      }

      if (DeviceAccessState.CurrentDevice.Platform
          is not SystemPlatform.Windows
          and not SystemPlatform.MacOs
          and not SystemPlatform.Linux)
      {
        return SignalingState.UnsupportedOperatingSystem;
      }

      return _systemSessions is not null
        ? SignalingState.SessionSelect
        : SignalingState.Unknown;
    }
  }

  private bool IsRemoteDisplayVisible => CurrentState == SignalingState.ConnectionActive;

  private string OuterDivClass
  {
    get
    {
      return CurrentState == SignalingState.ConnectionActive
        ? "h-100"
        : "h-100 ma-4";
    }
  }


  protected override async Task OnInitializedAsync()
  {
    await base.OnInitializedAsync();
    try
    {
      if (CurrentState == SignalingState.ConnectionActive)
      {
        return;
      }

      await GetDeviceDesktopSessions(false);
    }
    catch (Exception ex)
    {
      const string message = "Failed to retrieve desktop sessions on remote device.";
      Logger.LogError(ex, message);
      _alertMessage = message;
      _alertSeverity = Severity.Error;
    }
    finally
    {
      _loadingMessage = null;
    }
  }


  private async Task GetDeviceDesktopSessions(bool quiet)
  {
    try
    {
      _systemSessions = await ViewerHub.Server.GetActiveDesktopSessions(DeviceAccessState.CurrentDevice.Id);
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Failed to get active sessions.");
      if (!quiet)
      {
        Snackbar.Add("Failed to get active sessions", Severity.Warning);
        _alertMessage = "Failed to get active sessions.";
        _alertSeverity = Severity.Warning;
      }
    }
  }

  private async Task HandleDisconnectRequested()
  {
    try
    {
      RemoteControlState.ConnectionClosedRegistration?.Dispose();
      _systemSessions = [];
      RemoteControlState.CurrentSession = null;
      _loadingMessage = "Refreshing sessions";
      using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
      await StreamingClient.SendCloseStreamingSession(cts.Token);
      await StreamingClient.Close();
      await ScreenWake.SetScreenWakeLock(false);
      Snackbar.Add("Remote control session disconnected", Severity.Info);
      await GetDeviceDesktopSessions(false);
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while disconnecting from remote control session.");
      _alertMessage = "An error occurred while disconnecting from the remote control session.";
    }
    finally
    {
      _loadingMessage = null;
    }
  }

  private async Task HandleRefreshSessionsClicked()
  {
    await GetDeviceDesktopSessions(false);
    Snackbar.Add("Sessions refreshed", Severity.Info);
  }

  private async Task HandleReloadClicked()
  {
    _alertMessage = null;
    _loadingMessage = null;
    await GetDeviceDesktopSessions(false);
  }

  private async Task HandleStreamingConnectionLost()
  {
    if (await Reconnect())
    {
      return;
    }

    RemoteControlState.CurrentSession = null;
    Snackbar.Add("Connection lost", Severity.Warning);
    await GetDeviceDesktopSessions(false);
    await ScreenWake.SetScreenWakeLock(false);
    await InvokeAsync(StateHasChanged);
  }

  private async Task PreviewSession(DesktopSession desktopSession)
  {
    try
    {
      var parameters = new DialogParameters
      {
        { nameof(DesktopPreviewDialog.Device), DeviceAccessState.CurrentDevice },
        { nameof(DesktopPreviewDialog.Session), desktopSession }
      };

      var dialogOptions = new DialogOptions
      {
        BackdropClick = false,
        FullWidth = true,
        MaxWidth = MaxWidth.ExtraExtraLarge,
        FullScreen = CurrentBreakpoint < Breakpoint.Md,
        CloseOnEscapeKey = true
      };

      await DialogService.ShowAsync<DesktopPreviewDialog>(
        $"Desktop Preview - {desktopSession.Name}",
        parameters,
        dialogOptions);
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while requesting remote control session preview.");
      Snackbar.Add("Error while requesting session preview", Severity.Error);
      await InvokeAsync(StateHasChanged);
    }
  }

  private async Task<bool> Reconnect()
  {
    try
    {
      _isReconnecting = true;
      _alertMessage = null;
      _loadingMessage = null;

      await InvokeAsync(StateHasChanged);

      for (var i = 0; i < 5; i++)
      {
        try
        {
          if (i > 0)
          {
            await Task.Delay(3_000);
          }

          await GetDeviceDesktopSessions(true);
          if (_systemSessions is null)
          {
            continue;
          }

          if (_systemSessions.Length > 1)
          {
            break;
          }

          if (await StartRemoteControl(_systemSessions[0], true))
          {
            return true;
          }
        }
        catch (Exception ex)
        {
          Logger.LogError(ex, "Error while reconnecting.");
        }
      }

      _alertMessage = "Failed to reconnect.";
      _alertSeverity = Severity.Warning;
      return false;
    }
    finally
    {
      _isReconnecting = false;
      await InvokeAsync(StateHasChanged);
    }
  }

  private async Task<bool> StartRemoteControl(DesktopSession desktopSession, bool quiet)
  {
    try
    {
      if (!quiet)
      {
        _loadingMessage = "Starting remote control session";
      }

      _systemSessions = null;
      
      var session = new RemoteControlSession(
        DeviceAccessState.CurrentDevice,
        desktopSession.SystemSessionId,
        desktopSession.ProcessId,
        desktopSession.Type);

      var accessToken = RandomGenerator.CreateAccessToken();

      var serverUri = new Uri(NavManager.BaseUri).ToWebsocketUri();
      var uriBuilder = new UriBuilder(serverUri)
      {
        Path = "/relay",
        Query = $"?sessionId={session.SessionId}&accessToken={accessToken}&timeout={_commandTimeoutSeconds}"
      };

      if (!quiet)
      {
        Snackbar.Add($"Starting remote control in system session {desktopSession.SystemSessionId}", Severity.Info);
      }

      var notifyUser = await UserSettings.GetNotifyUserOnSessionStart();
      var requestDto = new RemoteControlSessionRequestDto(
        session.SessionId,
        uriBuilder.Uri,
        session.TargetSystemSession,
        session.TargetProcessId,
        string.Empty, // ViewerConnectionId is set by hub.
        session.Device.Id,
        notifyUser,
        RequireConsent: false);

      var streamingSessionResult = await ViewerHub.Server.RequestStreamingSession(session.Device.Id, requestDto);

      _loadingMessage = null;

      if (!streamingSessionResult.IsSuccess)
      {
        _alertMessage = streamingSessionResult.Reason;
        _alertSeverity = Severity.Error;
        await InvokeAsync(StateHasChanged);
        return false;
      }

      using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_commandTimeoutSeconds));
      await StreamingClient.Connect(uriBuilder.Uri, cts.Token);
      RemoteControlState.ConnectionClosedRegistration?.Dispose();
      RemoteControlState.ConnectionClosedRegistration = StreamingClient.OnClosed(HandleStreamingConnectionLost);
      RemoteControlState.CurrentSession = session;
      _loadingMessage = null;
      await ScreenWake.SetScreenWakeLock(true);
      return true;
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while requesting remote control session.");
      if (!quiet)
      {
        _alertMessage = "An error occurred while requesting the remote control session.";
        _alertSeverity = Severity.Error;
      }

      RemoteControlState.CurrentSession = null;
      return false;
    }
  }

  private enum SignalingState
  {
    Unknown,
    Loading,
    Alert,
    SessionSelect,
    ConnectionActive,
    Reconnecting,
    UnsupportedOperatingSystem
  }
}