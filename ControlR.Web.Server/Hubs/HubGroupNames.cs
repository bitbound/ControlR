namespace ControlR.Web.Server.Hubs;

public static class HubGroupNames
{
  public const string ServerAdministrators = "server-administrators";

  public static string GetDeviceGroupName(Guid deviceId)
  {
    return $"device-{deviceId}";
  }

  public static string GetUserRoleGroupName(string roleName, Guid tenantId)
  {
    return $"tenant-{tenantId}-user-role-{roleName}";
  }
}