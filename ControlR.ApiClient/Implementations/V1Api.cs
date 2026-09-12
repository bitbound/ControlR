using ControlR.ApiClient.Interfaces.V1;

namespace ControlR.ApiClient;

internal partial class V1Api(ControlrApi client) :
  IControlrV1Api,
  IAuthorizationChangeLogsApi,
  ICustomersApi,
  IDeploymentOptionsApi,
  IDeviceFileSystemApi,
  IDeviceGroupsApi,
  IDeviceTagsApi,
  IDevicesApi,
  IEffectivePermissionsApi,
  IEffectiveUserPreferencesApi,
  IInstallerKeysApi,
  IInvitesApi,
  ILogonTokensApi,
  IPermissionAssignmentsApi,
  IPersonalAccessTokensApi,
  IPublicServerSettingsApi,
  IServerAlertApi,
  IServerLogsApi,
  IServerServiceAccountsApi,
  IServerStatsApi,
  ITagsApi,
  ITenantServiceAccountsApi,
  ITenantsApi,
  ITenantSettingsApi,
  ITestEmailApi,
  IUserGroupsApi,
  IUserPreferencesApi,
  IUserServerSettingsApi,
  IUsersApi,
  IUserStorageApi,
  IVersionApi
{
  private readonly ControlrApi _client = client;

  public IAuthorizationChangeLogsApi AuthorizationChangeLogs => this;
  public ICustomersApi Customers => this;
  public IDeploymentOptionsApi DeploymentOptions => this;
  public IDeviceFileSystemApi DeviceFileSystem => this;
  public IDeviceGroupsApi DeviceGroups => this;
  public IDevicesApi Devices => this;
  public IDeviceTagsApi DeviceTags => this;
  public IEffectivePermissionsApi EffectivePermissions => this;
  public IEffectiveUserPreferencesApi EffectiveUserPreferences => this;
  public IInstallerKeysApi InstallerKeys => this;
  public IInvitesApi Invites => this;
  public ILogonTokensApi LogonTokens => this;
  public IPermissionAssignmentsApi PermissionAssignments => this;
  public IPersonalAccessTokensApi PersonalAccessTokens => this;
  public IPublicServerSettingsApi PublicServerSettings => this;
  public IServerAlertApi ServerAlert => this;
  public IServerLogsApi ServerLogs => this;
  public IServerServiceAccountsApi ServerServiceAccounts => this;
  public IServerStatsApi ServerStats => this;
  public ITagsApi Tags => this;
  public ITenantsApi Tenants => this;
  public ITenantServiceAccountsApi TenantServiceAccounts => this;
  public ITenantSettingsApi TenantSettings => this;
  public ITestEmailApi TestEmail => this;
  public IUserGroupsApi UserGroups => this;
  public IUserPreferencesApi UserPreferences => this;
  public IUsersApi Users => this;
  public IUserServerSettingsApi UserServerSettings => this;
  public IUserStorageApi UserStorage => this;
  public IVersionApi Version => this;
}