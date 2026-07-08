namespace ControlR.ApiClient;

internal partial class InternalApi(ControlrApi client) :
  IControlrInternalApi,
  IAuthApi,
  IDesktopPreviewApi,
  IDeviceFileSystemApi,
  IDeviceTagsApi,
  IDevicesApi,
  IEffectiveUserPreferencesApi,
  IInstallerKeysApi,
  IInvitesApi,
  ILogonTokensApi,
  IPersonalAccessTokensApi,
  IPublicRegistrationSettingsApi,
  IRolesApi,
  IServerAlertApi,
  IServerLogsApi,
  IServerStatsApi,
  IServiceAccountsApi,
  ITagsApi,
  ITenantSettingsApi,
  ITestEmailApi,
  IUserPreferencesApi,
  IUserRolesApi,
  IUsersApi,
  IUserServerSettingsApi,
  IUserStorageApi,
  IUserTagsApi,
  IVersionApi
{
  private readonly ControlrApi _client = client;

  public IAuthApi Auth => this;
  public IDesktopPreviewApi DesktopPreview => this;
  public IDeviceFileSystemApi DeviceFileSystem => this;
  public IDevicesApi Devices => this;
  public IDeviceTagsApi DeviceTags => this;
  public IEffectiveUserPreferencesApi EffectiveUserPreferences => this;
  public IInstallerKeysApi InstallerKeys => this;
  public IInvitesApi Invites => this;
  public ILogonTokensApi LogonTokens => this;
  public IPersonalAccessTokensApi PersonalAccessTokens => this;
  public IPublicRegistrationSettingsApi PublicRegistrationSettings => this;
  public IRolesApi Roles => this;
  public IServerAlertApi ServerAlert => this;
  public IServerLogsApi ServerLogs => this;
  public IServerStatsApi ServerStats => this;
  public IServiceAccountsApi ServiceAccounts => this;
  public ITagsApi Tags => this;
  public ITenantSettingsApi TenantSettings => this;
  public ITestEmailApi TestEmail => this;
  public IUserPreferencesApi UserPreferences => this;
  public IUserRolesApi UserRoles => this;
  public IUsersApi Users => this;
  public IUserServerSettingsApi UserServerSettings => this;
  public IUserStorageApi UserStorage => this;
  public IUserTagsApi UserTags => this;
  public IVersionApi Version => this;
}