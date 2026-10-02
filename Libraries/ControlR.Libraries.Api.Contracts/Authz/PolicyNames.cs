namespace ControlR.Libraries.Api.Contracts.Authz;

/// <summary>
/// Names of the permission-based authorization policies.
/// </summary>
public static class PolicyNames
{
  public const string RequireAgentInstall = "RequireAgentInstall";
  public const string RequireAuthorizationLogsRead = "RequireAuthorizationLogsRead";
  public const string RequireCustomersRead = "RequireCustomersRead";
  public const string RequireCustomersWrite = "RequireCustomersWrite";
  public const string RequireDeviceGroupAssignDevices = "RequireDeviceGroupAssignDevices";
  public const string RequireDeviceGroupsRead = "RequireDeviceGroupsRead";
  public const string RequireDeviceGroupsWrite = "RequireDeviceGroupsWrite";
  public const string RequireInstallerKeyRead = "RequireInstallerKeyRead";
  public const string RequireInstallerKeySelfWrite = "RequireInstallerKeySelfWrite";
  public const string RequireInstallerKeyWrite = "RequireInstallerKeyWrite";
  public const string RequirePermissionAssignmentsRead = "RequirePermissionAssignmentsRead";
  public const string RequirePermissionAssignmentsWrite = "RequirePermissionAssignmentsWrite";
  public const string RequirePersonalAccessTokenSelfRead = "RequirePersonalAccessTokenSelfRead";
  public const string RequirePersonalAccessTokenSelfWrite = "RequirePersonalAccessTokenSelfWrite";
  public const string RequirePersonalAccessTokensOthersRead = "RequirePersonalAccessTokensOthersRead";
  public const string RequirePersonalAccessTokensOthersWrite = "RequirePersonalAccessTokensOthersWrite";
  public const string RequireServerAuthorizationLogsRead = "RequireServerAuthorizationLogsRead";
  public const string RequireServerPermissionsRead = "RequireServerPermissionsRead";
  public const string RequireServerPermissionsWrite = "RequireServerPermissionsWrite";
  public const string RequireServerServiceAccountsRead = "RequireServerServiceAccountsRead";
  public const string RequireServerServiceAccountsRotateCredentials = "RequireServerServiceAccountsRotateCredentials";
  public const string RequireServerServiceAccountsWrite = "RequireServerServiceAccountsWrite";
  public const string RequireServerSettingsWrite = "RequireServerSettingsWrite";
  public const string RequireServerTelemetryRead = "RequireServerTelemetryRead";
  public const string RequireServerTenantsDelete = "RequireServerTenantsDelete";
  public const string RequireServerTenantsRead = "RequireServerTenantsRead";
  public const string RequireServerTenantsWrite = "RequireServerTenantsWrite";
  public const string RequireServiceAccountRead = "RequireServiceAccountRead";
  public const string RequireServiceAccountRotateCredentials = "RequireServiceAccountRotateCredentials";
  public const string RequireServiceAccountWrite = "RequireServiceAccountWrite";
  public const string RequireTagsWrite = "RequireTagsWrite";
  public const string RequireTenantSettingsRead = "RequireTenantSettingsRead";
  public const string RequireTenantSettingsWrite = "RequireTenantSettingsWrite";
  public const string RequireTenantUsersDelete = "RequireTenantUsersDelete";
  public const string RequireTenantUsersWrite = "RequireTenantUsersWrite";
  public const string RequireUserGroupAssignUsers = "RequireUserGroupAssignUsers";
  public const string RequireUserGroupsRead = "RequireUserGroupsRead";
  public const string RequireUserGroupsWrite = "RequireUserGroupsWrite";
  public const string RequireUsersRead = "RequireUsersRead";
}
