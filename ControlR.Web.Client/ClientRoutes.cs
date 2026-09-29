namespace ControlR.Web.Client;

public static class ClientRoutes
{
  public const string About = "/about";
  public const string AuthorizationLogs = "/authorization-logs";
  public const string Customers = "/customers";
  public const string Deploy = "/deploy";
  public const string DeviceAccess = "/device-access";
  public const string DeviceAccessChat = $"{DeviceAccess}/chat";
  public const string DeviceAccessFileSystem = $"{DeviceAccess}/file-system";
  public const string DeviceAccessRemoteControl = $"{DeviceAccess}/remote-control";
  public const string DeviceAccessRemoteLogs = $"{DeviceAccess}/remote-logs";
  public const string DeviceAccessTerminal = $"{DeviceAccess}/terminal";
  public const string DeviceAccessVncRelay = $"{DeviceAccess}/vnc-relay";
  public const string DeviceGroupDetail = "/device-groups/{Id:guid}";
  public const string DeviceGroups = "/device-groups";
  public const string EffectivePermissions = "/effective-permissions";
  public const string Home = "/";
  public const string InstallerKeys = "/installer-keys";
  public const string Invite = "/invite";
  public const string InviteConfirmation = InviteConfirmationBase + "/{activationCode?}";
  public const string InviteConfirmationBase = "/invite-confirmation";
  public const string NotFound = "/not-found";
  public const string PasswordChangeRequired = "/password-change-required";
  public const string Permissions = "/permissions";
  public const string PersonalAccessTokens = "/personal-access-tokens";
  public const string ServerAuthorizationLogs = "/server-authorization-logs";
  public const string ServerLogs = "/server-logs";
  public const string ServerServiceAccounts = "/server-service-accounts";
  public const string ServerSettings = "/server-settings";
  public const string ServerStats = "/server-stats";
  public const string Settings = "/settings";
  public const string Tags = "/tags";
  public const string TenantServiceAccounts = "/tenant-service-accounts";
  public const string TenantSettings = "/tenant-settings";
  public const string Unauthorized = "/unauthorized";
  public const string UserGroupDetail = "/user-groups/{Id:guid}";
  public const string UserGroups = "/user-groups";
  public const string Users = "/users";
}
