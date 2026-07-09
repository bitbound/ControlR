namespace ControlR.Libraries.Api.Contracts.Constants;

public static class HttpConstants
{
  public const string AgentUpdateEndpoint = "/api/agent-update";
  public const string AuthEndpoint = "/api/auth";
  public const string DesktopPreviewEndpoint = "/api/desktop-preview";
  public const string DeviceFileSystemEndpoint = "/api/device-file-system";
  public const string DevicesEndpoint = "/api/devices";
  public const string DeviceTagsEndpoint = "/api/device-tags";
  public const string EffectiveUserPreferencesEndpoint = "/api/effective-user-preferences";
  public const string InstallerKeysEndpoint = "/api/installer-keys";
  public const string InvitesEndpoint = "/api/invites";
  public const string LogonTokensEndpoint = "/api/logon-tokens";
  public const string PersonalAccessTokensEndpoint = "/api/personal-access-tokens";
  public const string PublicRegistrationSettingsEndpoint = "/api/public-registration-settings";
  public const string RolesEndpoint = "/api/roles";
  public const string ServerAlertEndpoint = "/api/server-alert";
  public const string ServerLogsEndpoint = "/api/server-logs";
  public const string ServerStatsEndpoint = "/api/server-stats";
  public const string ServiceAccountsEndpoint = "/api/service-accounts";
  public const string TagsEndpoint = "/api/tags";
  public const string TenantsEndpoint = "/api/tenants";
  public const string TenantSettingsEndpoint = "/api/tenant-settings";
  public const string TestEmailEndpoint = "/api/test-email";
  public const string UserPreferencesEndpoint = "/api/user-preferences";
  public const string UserRolesEndpoint = "/api/user-roles";
  public const string UsersEndpoint = "/api/users";
  public const string UserServerSettingsEndpoint = "/api/user-server-settings";
  public const string UserStorageEndpoint = "/api/user-storage";
  public const string UserTagsEndpoint = "/api/user-tags";
  public const string VersionEndpoint = "/api/version";

  public static class Internal
  {
    public const string AgentUpdateEndpoint = "/internal/agent-update";
    public const string InstallerKeysEndpoint = "/internal/installer-keys";
    public const string InvitesEndpoint = "/internal/invites";
    public const string LogonTokensEndpoint = "/internal/logon-tokens";
    public const string TenantSettingsEndpoint = "/internal/tenant-settings";
    public const string UsersEndpoint = "/internal/users";
  }

  public static class V0
  {
    public const string DevicesEndpoint = "/v0/devices";
    public const string InstallerKeysEndpoint = "/v0/installer-keys";
    public const string LogonTokensEndpoint = "/v0/logon-tokens";
    public const string TenantsEndpoint = "/v0/tenants";
  }
}