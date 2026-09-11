namespace ControlR.ApiClient.Interfaces.V1;

public interface IControlrV1Api
{
  IAuthorizationChangeLogsApi AuthorizationChangeLogs { get; }
  ICustomersApi Customers { get; }
  IDeploymentOptionsApi DeploymentOptions { get; }
  IDeviceGroupsApi DeviceGroups { get; }
  IDevicesApi Devices { get; }
  IDeviceTagsApi DeviceTags { get; }
  IEffectivePermissionsApi EffectivePermissions { get; }
  IInstallerKeysApi InstallerKeys { get; }
  ILogonTokensApi LogonTokens { get; }
  IPermissionAssignmentsApi PermissionAssignments { get; }
  IServerServiceAccountsApi ServerServiceAccounts { get; }
  ITagsApi Tags { get; }
  ITenantsApi Tenants { get; }
  ITenantServiceAccountsApi TenantServiceAccounts { get; }
  IUserGroupsApi UserGroups { get; }
}