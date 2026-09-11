using ControlR.ApiClient.Interfaces.Internal;

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
  public IPublicServerSettingsApi PublicServerSettings => this;
  public IServerAlertApi ServerAlert => this;
  public IServerLogsApi ServerLogs => this;
  public IServerStatsApi ServerStats => this;
  public ITagsApi Tags => this;
  public ITenantServiceAccountsApi TenantServiceAccounts => this;
  public ITenantSettingsApi TenantSettings => this;
  public ITestEmailApi TestEmail => this;
  public IUserPreferencesApi UserPreferences => this;
  public IUsersApi Users => this;
  public IUserServerSettingsApi UserServerSettings => this;
  public IUserStorageApi UserStorage => this;
  public IVersionApi Version => this;
}