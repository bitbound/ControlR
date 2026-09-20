namespace ControlR.Web.Server.Hubs;

public static class HubGroupNames
{
  public static string DeviceHeartbeat(Guid deviceId) => $"device:{deviceId}:heartbeat";
}
