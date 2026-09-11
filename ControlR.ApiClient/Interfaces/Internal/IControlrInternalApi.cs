namespace ControlR.ApiClient.Interfaces.Internal;

public interface IControlrInternalApi
{
  IAuthApi Auth { get; }
  IDesktopPreviewApi DesktopPreview { get; }
  IDeviceFileSystemApi DeviceFileSystem { get; }
  IDevicesApi Devices { get; }
  IDeviceTagsApi DeviceTags { get; }
  IEffectiveUserPreferencesApi EffectiveUserPreferences { get; }
  IInstallerKeysApi InstallerKeys { get; }
  IInvitesApi Invites { get; }
  ILogonTokensApi LogonTokens { get; }
  IPersonalAccessTokensApi PersonalAccessTokens { get; }
  IPublicServerSettingsApi PublicServerSettings { get; }
  IServerAlertApi ServerAlert { get; }
  IServerLogsApi ServerLogs { get; }
  IServerStatsApi ServerStats { get; }
  ITagsApi Tags { get; }
  ITenantSettingsApi TenantSettings { get; }
  ITestEmailApi TestEmail { get; }
  IUserPreferencesApi UserPreferences { get; }
  IUsersApi Users { get; }
  IUserServerSettingsApi UserServerSettings { get; }
  IUserStorageApi UserStorage { get; }
  IVersionApi Version { get; }
}