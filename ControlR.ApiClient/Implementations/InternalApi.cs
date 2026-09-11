using ControlR.ApiClient.Interfaces.Internal;

namespace ControlR.ApiClient;

internal partial class InternalApi(ControlrApi client) :
  IControlrInternalApi,
  IAuthApi,
  IAuthorizationChangeLogsApi,
  ICustomersApi,
  IDesktopPreviewApi,
  IDeviceFileSystemApi,
  IDeviceGroupsApi,
  IDeviceTagsApi,
  IDevicesApi,
  IDeploymentOptionsApi,
  IEffectivePermissionsApi,
  IEffectiveUserPreferencesApi,
  IInstallerKeysApi,
  IInvitesApi,
  ILogonTokensApi,
  IPersonalAccessTokensApi,
  IPermissionAssignmentsApi,
  IPublicServerSettingsApi,
  IServerAlertApi,
  IServerLogsApi,
  IServerStatsApi,
  ITenantServiceAccountsApi,
  ITagsApi,
  ITenantSettingsApi,
  ITestEmailApi,
  IUserPreferencesApi,
  IUsersApi,
  IUserServerSettingsApi,
  IUserStorageApi,
  IUserGroupsApi,
  IVersionApi
{
  private readonly ControlrApi _client = client;

  public IAuthApi Auth => this;
  public IAuthorizationChangeLogsApi AuthorizationChangeLogs => this;
  public ICustomersApi Customers => this;
  public IDeploymentOptionsApi DeploymentOptions => this;
  public IDesktopPreviewApi DesktopPreview => this;
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
  public IServerStatsApi ServerStats => this;
  public ITagsApi Tags => this;
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