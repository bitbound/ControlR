using ControlR.ApiClient.Interfaces.V1;

namespace ControlR.ApiClient;

internal partial class V1Api(ControlrApi client) :
  IControlrV1Api,
  IAuthorizationChangeLogsApi,
  ICustomersApi,
  IDeploymentOptionsApi,
  IDeviceGroupsApi,
  IDeviceTagsApi,
  IDevicesApi,
  IEffectivePermissionsApi,
  IInstallerKeysApi,
  IInvitesApi,
  ILogonTokensApi,
  IPermissionAssignmentsApi,
  IPersonalAccessTokensApi,
  IServerServiceAccountsApi,
  ITagsApi,
  ITenantServiceAccountsApi,
  ITenantsApi,
  ITenantSettingsApi,
  IUserGroupsApi,
  IUsersApi
{
  private readonly ControlrApi _client = client;

  public IAuthorizationChangeLogsApi AuthorizationChangeLogs => this;
  public ICustomersApi Customers => this;
  public IDeploymentOptionsApi DeploymentOptions => this;
  public IDeviceGroupsApi DeviceGroups => this;
  public IDevicesApi Devices => this;
  public IDeviceTagsApi DeviceTags => this;
  public IEffectivePermissionsApi EffectivePermissions => this;
  public IInstallerKeysApi InstallerKeys => this;
  public IInvitesApi Invites => this;
  public ILogonTokensApi LogonTokens => this;
  public IPermissionAssignmentsApi PermissionAssignments => this;
  public IPersonalAccessTokensApi PersonalAccessTokens => this;
  public IServerServiceAccountsApi ServerServiceAccounts => this;
  public ITagsApi Tags => this;
  public ITenantsApi Tenants => this;
  public ITenantServiceAccountsApi TenantServiceAccounts => this;
  public ITenantSettingsApi TenantSettings => this;
  public IUserGroupsApi UserGroups => this;
  public IUsersApi Users => this;
}