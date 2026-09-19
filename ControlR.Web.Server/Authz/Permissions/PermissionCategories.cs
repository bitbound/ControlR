namespace ControlR.Web.Server.Authz.Permissions;

/// <summary>
/// The category labels a permission can be grouped under in the UI. A permission's label is declared
/// where the permission is added, so nothing derives it from the permission name.
/// </summary>
public static class PermissionCategories
{
  public const string Agents = "Agents";
  public const string DeviceGroups = "Device Groups";
  public const string Devices = "Devices";
  public const string InstallerKeys = "Installer Keys";
  public const string PersonalAccessTokens = "Personal Access Tokens";
  public const string Servers = "Servers";
  public const string ServiceAccounts = "Service Accounts";
  public const string Tenants = "Tenants";
  public const string UserGroups = "User Groups";
}
