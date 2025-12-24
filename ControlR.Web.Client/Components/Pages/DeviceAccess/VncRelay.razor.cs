using ControlR.Libraries.Shared.Dtos.RemoteControlDtos;
using ControlR.Libraries.WebSocketRelay.Client;
using ControlR.Web.Client.StateManagement.DeviceAccess;
using Microsoft.AspNetCore.Components;

namespace ControlR.Web.Client.Components.Pages.DeviceAccess;

public partial class VncRelay
{
  private bool _loading = false;
  private Uri? _noVncUri;
  private int _port = 5900;

  [Inject]
  public required IDeviceState DeviceAccessState { get; init; }
  [Inject]
  public required ILogger<VncRelay> Logger { get; init; }
  [Inject]
  public required NavigationManager NavManager { get; init; }
  [Inject]
  public required ISnackbar Snackbar { get; init; }
  [Inject]
  public required ITenantSettingsProvider TenantSettings { get; init; }
  [Inject]
  public required IUserSettingsProvider UserSettings { get; init; }
  [Inject]
  public required IHubConnection<IViewerHub> ViewerHub { get; init; }

  private void Disconnect()
  {
    _noVncUri = null;
    _loading = false;
  }

  private async Task RequestStreamingSessionFromAgent()
  {
    if (DeviceAccessState.CurrentDeviceMaybe is not { } device)
    {
      Snackbar.Add("No device selected", Severity.Error);
      return;
    }

    try
    {
      _loading = true;
      Snackbar.Add("Connecting", Severity.Info);
      await InvokeAsync(StateHasChanged);

      var sessionId = Guid.NewGuid();
      var accessToken = RandomGenerator.CreateAccessToken();

      var serverUri = new Uri(NavManager.BaseUri);

      var viewerRelayUri = RelayUriBuilder.Build(
          baseUri: serverUri.ToWebsocketUri(),
          path: AppConstants.WebSocketRelayPath,
          sessionId: sessionId,
          accessToken: accessToken,
          role: RelayRole.Requester,
          timeoutSeconds: 30);

      var encodedPath = Uri.EscapeDataString(viewerRelayUri.PathAndQuery);

      _noVncUri = new Uri(serverUri,
          $"novnc/vnc.html?path={encodedPath}&resize=scale&autoconnect=true");

      Logger.LogInformation("Resolved NoVNC relay URI: {NoVncUri}", _noVncUri);
      Logger.LogInformation("Creating streaming session.");

      var tenantNotifyUser = await TenantSettings.GetNotifyUserOnSessionStart();
      var notifyUser = tenantNotifyUser ?? await UserSettings.GetNotifyUserOnSessionStart();

      var deviceRelayUri = RelayUriBuilder.Build(
          baseUri: serverUri.ToWebsocketUri(),
          path: AppConstants.WebSocketRelayPath,
          sessionId: sessionId,
          accessToken: accessToken,
          role: RelayRole.Responder,
          timeoutSeconds: 30);

      Logger.LogInformation("Resolved device relay URI: {DeviceRelayUri}", deviceRelayUri);

      var requestDto = new VncSessionRequestDto(
          sessionId,
          deviceRelayUri,
          ViewerHub.ConnectionId ?? string.Empty,
          device.Id,
          notifyUser,
          _port);

      var sessionResult = await ViewerHub.Server.RequestVncSession(device.Id, requestDto);

      if (!sessionResult.IsSuccess)
      {
        Snackbar.Add(sessionResult.Reason, Severity.Error);
        Disconnect();
        return;
      }
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while requesting streaming session.");
      Snackbar.Add("An error occurred while requesting streaming session", Severity.Error);
      Disconnect();
    }
    finally
    {
      _loading = false;
      await InvokeAsync(StateHasChanged);
    }
  }

  private async Task TestConnection()
  {
    try
    {
      if (DeviceAccessState.CurrentDeviceMaybe is not { } device)
      {
        Snackbar.Add("No device selected", Severity.Error);
        return;
      }
      Snackbar.Add("Testing connection", Severity.Info);
      var testResult = await ViewerHub.Server.TestVncConnection(device.Id, _port);
      if (testResult.IsSuccess)
      {
        Snackbar.Add("Connection test successful", Severity.Success);
      }
      else
      {
        Snackbar.Add($"Test failed.  Reason: {testResult.Reason}", Severity.Error);
      }
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while testing connection.");
      Snackbar.Add("An error occurred while testing connection", Severity.Error);
    }
  }
}
