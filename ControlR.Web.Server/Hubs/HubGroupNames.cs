namespace ControlR.Web.Server.Hubs;

public static class HubGroupNames
{
  public const string ServerAdministrators = "server-administrators";

  public static string GetDeviceGroupName(Guid deviceId, Guid tenantId)
  {
    return $"tenant-{tenantId}-device-{deviceId}";
  }

  public static string GetTagGroupName(Guid tagId, Guid tenantId)
  {
    return $"tenant-{tenantId}-tag-{tagId}";
  }

  public static string GetUserRoleGroupName(string roleName, Guid tenantId)
  {
    return $"tenant-{tenantId}-user-role-{roleName}";
  }
}