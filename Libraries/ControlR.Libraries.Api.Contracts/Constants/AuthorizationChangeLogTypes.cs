namespace ControlR.Libraries.Api.Contracts.Constants;

/// <summary>
/// Well-known action types for authorization change log entries.
/// Stored as human-readable strings in the database for raw inspection.
/// </summary>
public static class AuthorizationChangeLogActions
{
  public const string CredentialScopeRemoved = "credential-scope-removed";
  public const string CredentialScopeSet = "credential-scope-set";
  public const string CredentialScopeSetFailed = "credential-scope-set-failed";
  public const string CredentialScopeTrim = "credential-scope-trim";
  public const string CustomerCreated = "customer-created";
  public const string CustomerDeleted = "customer-deleted";
  public const string CustomerDevicesAssigned = "customer-devices-assigned";
  public const string CustomerUpdated = "customer-updated";
  public const string DeviceGroupCreated = "device-group-created";
  public const string DeviceGroupDeleted = "device-group-deleted";
  public const string DeviceGroupMembersAdded = "device-group-members-added";
  public const string DeviceGroupMembersRemoved = "device-group-members-removed";
  public const string DeviceGroupUpdated = "device-group-updated";
  public const string PermissionAssignmentCreated = "permission-assignment-created";
  public const string PermissionAssignmentDeleted = "permission-assignment-deleted";
  public const string PermissionAssignmentsSeeded = "permission-assignments-seeded";
  public const string PermissionAssignmentUpdated = "permission-assignment-updated";
  public const string ServiceAccountCreated = "service-account-created";
  public const string ServiceAccountCredentialCreated = "service-account-credential-created";
  public const string ServiceAccountCredentialDeleted = "service-account-credential-deleted";
  public const string ServiceAccountCredentialRevoked = "service-account-credential-revoked";
  public const string ServiceAccountDeleted = "service-account-deleted";
  public const string ServiceAccountUpdated = "service-account-updated";
  public const string UserGroupCreated = "user-group-created";
  public const string UserGroupDeleted = "user-group-deleted";
  public const string UserGroupMembersAdded = "user-group-members-added";
  public const string UserGroupMembersRemoved = "user-group-members-removed";
  public const string UserGroupUpdated = "user-group-updated";
}

/// <summary>
/// Well-known actor principal types for authorization change log entries.
/// Mirrors the canonical principal-type claim values.
/// </summary>
public static class AuthorizationChangeLogActorTypes
{
  public const string ServiceAccount = "service-account";
  public const string System = "system";
  public const string User = "user";
}

/// <summary>
/// Well-known target types for authorization change log entries.
/// </summary>
public static class AuthorizationChangeLogTargetTypes
{
  public const string Customer = "Customer";
  public const string DeviceGroup = "DeviceGroup";
  public const string LogonToken = "LogonToken";
  public const string PermissionAssignment = "PermissionAssignment";
  public const string PersonalAccessToken = "PersonalAccessToken";
  public const string ServiceAccount = "ServiceAccount";
  public const string ServiceAccountCredential = "ServiceAccountCredential";
  public const string User = "User";
  public const string UserGroup = "UserGroup";
}
