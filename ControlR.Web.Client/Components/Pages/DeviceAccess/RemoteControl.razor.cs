using System.Net.WebSockets;
using ControlR.Libraries.Shared.Dtos.StreamerDtos;
using ControlR.Web.Client.Services.DeviceAccess;
using Microsoft.AspNetCore.Components;

namespace ControlR.Web.Client.Components.Pages.DeviceAccess;

// ReSharper disable once ClassNeverInstantiated.Global
public partial class RemoteControl : ComponentBase
{
  private string? _alertMessage;
  private Severity _alertSeverity;
  private double _downloadProgress = -1;
  private string? _downloadingMessage;
  private string? _loadingMessage = "Loading";

  private DesktopSession[]? _systemSessions;

  [Inject]
  public required IHubConnection<IDeviceAccessHub> DeviceAccessHub { get; init; }
  
  [Inject]
  public required IDeviceState DeviceAccessState { get; init; }

  [Inject]
  public required IDialogService DialogService { get; init; }

  [Inject]
  public required ILogger<RemoteControl> Logger { get; init; }

  [Inject]
  public required IMessenger Messenger { get; init; }

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
      if (StreamingClient.State == WebSocketState.Open)
      {
        return SignalingState.ConnectionActive;
      }

      if (!string.IsNullOrWhiteSpace(_downloadingMessage))
      {
        return SignalingState.Downloading;
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
      if (CurrentState == SignalingState.ConnectionActive)
      {
        return "h-100";
      }

      return "h-100 ma-4";
    }
  }

  protected override async Task OnInitializedAsync()
  {
    await base.OnInitializedAsync();
    try
    {
      Messenger.Register<DtoReceivedMessage<DesktopClientDownloadProgressDto>>(this, HandleStreamerDownloadProgress);

      if (CurrentState == SignalingState.ConnectionActive)
      {
        return;
      }

      await GetDeviceSystemSessions();
    }
    catch (Exception ex)
    {
      const string message = "Failed to retrieve system sessions on remote device.";
      Logger.LogError(ex, message);
      _alertMessage = message;
      _alertSeverity = Severity.Error;
    }
    finally
    {
      _loadingMessage = null;
    }
  }

  private async Task GetDeviceSystemSessions()
  {
    try
    {
        _systemSessions = await DeviceAccessHub.Server.GetActiveDesktopSessions(DeviceAccessState.CurrentDevice.Id);
    }
    catch (Exception ex)
    {
        Logger.LogError(ex, "Failed to get active sessions.");
        Snackbar.Add("Failed to get active sessions", Severity.Warning);
        _alertMessage = "Failed to get active sessions.";
        _alertSeverity = Severity.Warning;
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
      await ScreenWake.SetScreenWakeLock(isWakeEnabled: false);
      Snackbar.Add("Remote control session disconnected", Severity.Info);
      await GetDeviceSystemSessions();
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

  private async Task HandleStreamerDownloadProgress(
    object recipient,
    DtoReceivedMessage<DesktopClientDownloadProgressDto> message)
  {
    var dto = message.Dto;

    if (dto.RemoteControlSessionId != RemoteControlState.CurrentSession?.SessionId)
    {
      return;
    }

    _downloadProgress = dto.Progress;
    _downloadingMessage = dto.Message;

    await InvokeAsync(StateHasChanged);
  }

  private async Task HandleStreamingConnectionLost()
  {
    RemoteControlState.CurrentSession = null;
    Snackbar.Add("Connection lost", Severity.Warning);
    await GetDeviceSystemSessions();
    await ScreenWake.SetScreenWakeLock(isWakeEnabled: false);
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

  private async Task RefreshSystemSessions()
  {
    await GetDeviceSystemSessions();
    Snackbar.Add("Sessions refreshed", Severity.Info);
  }

  private async Task Reload()
  {
    _alertMessage = null;
    _loadingMessage = null;
    _downloadingMessage = null;
    _downloadProgress = -1;
     await GetDeviceSystemSessions();
  }

  private async Task StartRemoteControl(DesktopSession desktopSession)
  {
    try
    {
      _loadingMessage = "Starting remote control session";
      _systemSessions = null;

      var session = new RemoteControlSession(
        DeviceAccessState.CurrentDevice,
        desktopSession.SystemSessionId,
        desktopSession.ProcessId);

      var accessToken = RandomGenerator.CreateAccessToken();

      var serverUri = new Uri(NavManager.BaseUri).ToWebsocketUri();
      var uriBuilder = new UriBuilder(serverUri)
      {
        Path = "/relay",
        Query = $"?sessionId={session.SessionId}&accessToken={accessToken}&timeout=30"
      };

      Snackbar.Add($"Starting remote control in system session {desktopSession.SystemSessionId}", Severity.Info);

      var notifyUser = await UserSettings.GetNotifyUserOnSessionStart();
      var requestDto = new RemoteControlSessionRequestDto(
        session.SessionId,
        uriBuilder.Uri,
        session.TargetSystemSession,
        session.TargetProcessId,
        string.Empty, // ViewerConnectionId is set by hub.
        session.Device.Id,
        notifyUser);

      var streamingSessionResult = await DeviceAccessHub.Server.RequestStreamingSession(session.Device.Id, requestDto);

      _downloadingMessage = string.Empty;
      _downloadProgress = -1;
      _loadingMessage = null;

      if (!streamingSessionResult.IsSuccess)
      {
        _alertMessage = streamingSessionResult.Reason;
        _alertSeverity = Severity.Error;
        await InvokeAsync(StateHasChanged);
        return;
      }

      await StreamingClient.Connect(uriBuilder.Uri, CancellationToken.None);
      RemoteControlState.ConnectionClosedRegistration?.Dispose();
      RemoteControlState.ConnectionClosedRegistration = StreamingClient.OnClosed(HandleStreamingConnectionLost);
      RemoteControlState.CurrentSession = session;
      _loadingMessage = null;
      await ScreenWake.SetScreenWakeLock(isWakeEnabled: true);
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while requesting remote control session.");
      _alertMessage = "An error occurred while requesting the remote control session.";
      _alertSeverity = Severity.Error;
      RemoteControlState.CurrentSession = null;
    }
  }

  private enum SignalingState
  {
    Unknown,
    Loading,
    Downloading,
    Alert,
    SessionSelect,
    ConnectionActive,
    UnsupportedOperatingSystem
  }
}