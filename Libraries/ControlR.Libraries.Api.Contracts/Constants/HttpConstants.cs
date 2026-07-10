namespace ControlR.Libraries.Api.Contracts.Constants;

public static class HttpConstants
{
  public const string AgentUpdateEndpoint = "/api/agent-update";
  public const string DevicesEndpoint = "/api/devices";

  public static class Internal
  {
    public const string AgentUpdateEndpoint = "/api/internal/agent-update";
    public const string AuthEndpoint = "/api/internal/auth";
    public const string DesktopPreviewEndpoint = "/api/internal/desktop-preview";
    public const string DeviceFileSystemEndpoint = "/api/internal/device-file-system";
    public const string DevicesEndpoint = "/api/internal/devices";
    public const string DeviceTagsEndpoint = "/api/internal/device-tags";
    public const string EffectiveUserPreferencesEndpoint = "/api/internal/effective-user-preferences";
    public const string InstallerKeysEndpoint = "/api/internal/installer-keys";
    public const string InvitesEndpoint = "/api/internal/invites";
    public const string LogonTokensEndpoint = "/api/internal/logon-tokens";
    public const string PersonalAccessTokensEndpoint = "/api/internal/personal-access-tokens";
    public const string PublicRegistrationSettingsEndpoint = "/api/internal/public-registration-settings";
    public const string RolesEndpoint = "/api/internal/roles";
    public const string ServerAlertEndpoint = "/api/internal/server-alert";
    public const string ServerLogsEndpoint = "/api/internal/server-logs";
    public const string ServerStatsEndpoint = "/api/internal/server-stats";
    public const string TagsEndpoint = "/api/internal/tags";
    public const string TenantsEndpoint = "/api/internal/tenants";
    public const string TenantSettingsEndpoint = "/api/internal/tenant-settings";
    public const string TestEmailEndpoint = "/api/internal/test-email";
    public const string UserPreferencesEndpoint = "/api/internal/user-preferences";
    public const string UserRolesEndpoint = "/api/internal/user-roles";
    public const string UsersEndpoint = "/api/internal/users";
    public const string UserServerSettingsEndpoint = "/api/internal/user-server-settings";
    public const string UserStorageEndpoint = "/api/internal/user-storage";
    public const string UserTagsEndpoint = "/api/internal/user-tags";
    public const string VersionEndpoint = "/api/internal/version";
  }
  public static class V0
  {
    public const string DevicesEndpoint = "/api/v0/devices";
    public const string InstallerKeysEndpoint = "/api/v0/installer-keys";
    public const string LogonTokensEndpoint = "/api/v0/logon-tokens";
    public const string ServiceAccountsEndpoint = "/api/v0/service-accounts";
    public const string TenantsEndpoint = "/api/v0/tenants";
  }
}
