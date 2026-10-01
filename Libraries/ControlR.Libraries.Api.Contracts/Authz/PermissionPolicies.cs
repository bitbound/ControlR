using System.Collections.Frozen;

namespace ControlR.Libraries.Api.Contracts.Authz;

/// <summary>
/// Maps each permission-based policy to its permission and authorization resource scope. The
/// server registers these against the permission evaluator. Tenant- and server-scoped policies
/// are also projected to the Blazor client as policy-grant claims for declarative UI authorization.
/// </summary>
public static class PermissionPolicies
{
  /// <summary>
  /// Claim type for server-evaluated client policy grants. A claim of this type whose value
  /// is a <see cref="PolicyNames"/> entry means the server evaluated that policy against its
  /// canonical (tenant/server) resource while producing the current auth snapshot and the
  /// decision was allowed. A permission name may be resource-scoped, whereas a client policy
  /// grant answers one exact resource-independent client presentation question.
  /// </summary>
  public const string ClientPolicyClaimType = "controlr:client-policy";

  private static IReadOnlyDictionary<string, PermissionPolicyDefinition>? _clientDefinitions;

  /// <summary>
  /// The subset of <see cref="Definitions"/> projected to the client as
  /// <see cref="ClientPolicyClaimType"/> grants. A policy is projected when its canonical
  /// resource kind is <see cref="PermissionScopeKind.Tenant"/> or
  /// <see cref="PermissionScopeKind.Server"/>; resource-specific access is never represented
  /// as a global claim.
  /// </summary>
  public static IReadOnlyDictionary<string, PermissionPolicyDefinition> ClientDefinitions
  {
    get
    {
      return _clientDefinitions ??= Definitions
        .Where(entry => entry.Value.ResourceScopeKind is PermissionScopeKind.Tenant or PermissionScopeKind.Server)
        .ToFrozenDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
    }
  }
  public static IReadOnlyDictionary<string, PermissionPolicyDefinition> Definitions { get; } =
    new Dictionary<string, PermissionPolicyDefinition>
    {
      [PolicyNames.RequireAgentInstall] = new(PermissionNames.AgentInstall, PermissionScopeKind.Tenant),
      [PolicyNames.RequireAuthorizationLogsRead] = new(PermissionNames.TenantAuthorizationLogsRead, PermissionScopeKind.Tenant),
      [PolicyNames.RequireCustomersRead] = new(PermissionNames.TenantCustomersRead, PermissionScopeKind.Tenant),
      [PolicyNames.RequireCustomersWrite] = new(PermissionNames.TenantCustomersWrite, PermissionScopeKind.Tenant),
      [PolicyNames.RequireDeviceGroupAssignDevices] = new(PermissionNames.DeviceGroupAssignDevices, PermissionScopeKind.DeviceGroup),
      [PolicyNames.RequireDeviceGroupsRead] = new(PermissionNames.TenantDeviceGroupsRead, PermissionScopeKind.Tenant),
      [PolicyNames.RequireDeviceGroupsWrite] = new(PermissionNames.TenantDeviceGroupsWrite, PermissionScopeKind.Tenant),
      [PolicyNames.RequireInstallerKeySelfRead] = new(PermissionNames.InstallerKeySelfRead, PermissionScopeKind.Tenant),
      [PolicyNames.RequireInstallerKeySelfWrite] = new(PermissionNames.InstallerKeySelfWrite, PermissionScopeKind.Tenant),
      [PolicyNames.RequirePermissionAssignmentsRead] = new(PermissionNames.TenantPermissionsRead, PermissionScopeKind.Tenant),
      [PolicyNames.RequirePermissionAssignmentsWrite] = new(PermissionNames.TenantPermissionsWrite, PermissionScopeKind.Tenant),
      [PolicyNames.RequirePersonalAccessTokenSelfRead] = new(PermissionNames.PersonalAccessTokenSelfRead, PermissionScopeKind.Tenant),
      [PolicyNames.RequirePersonalAccessTokenSelfWrite] = new(PermissionNames.PersonalAccessTokenSelfWrite, PermissionScopeKind.Tenant),
      [PolicyNames.RequirePersonalAccessTokensOthersRead] = new(PermissionNames.PersonalAccessTokenOthersRead, PermissionScopeKind.Tenant),
      [PolicyNames.RequirePersonalAccessTokensOthersWrite] = new(PermissionNames.PersonalAccessTokenOthersWrite, PermissionScopeKind.Tenant),
      [PolicyNames.RequireServerAuthorizationLogsRead] = new(PermissionNames.ServerAuthorizationLogsRead, PermissionScopeKind.Server),
      [PolicyNames.RequireServerPermissionsRead] = new(PermissionNames.ServerPermissionsRead, PermissionScopeKind.Server),
      [PolicyNames.RequireServerPermissionsWrite] = new(PermissionNames.ServerPermissionsWrite, PermissionScopeKind.Server),
      [PolicyNames.RequireServerSettingsWrite] = new(PermissionNames.ServerSettingsWrite, PermissionScopeKind.Server),
      [PolicyNames.RequireServerTenantsDelete] = new(PermissionNames.ServerTenantsDelete, PermissionScopeKind.Server),
      [PolicyNames.RequireServerTenantsRead] = new(PermissionNames.ServerTenantsRead, PermissionScopeKind.Server),
      [PolicyNames.RequireServerTenantsWrite] = new(PermissionNames.ServerTenantsWrite, PermissionScopeKind.Server),
      [PolicyNames.RequireServerServiceAccountsRead] = new(PermissionNames.ServerServiceAccountsRead, PermissionScopeKind.Server),
      [PolicyNames.RequireServerServiceAccountsRotateCredentials] = new(PermissionNames.ServerServiceAccountsRotateCredentials, PermissionScopeKind.Server),
      [PolicyNames.RequireServerServiceAccountsWrite] = new(PermissionNames.ServerServiceAccountsWrite, PermissionScopeKind.Server),
      [PolicyNames.RequireServerTelemetryRead] = new(PermissionNames.ServerTelemetryRead, PermissionScopeKind.Server),
      [PolicyNames.RequireServiceAccountRead] = new(PermissionNames.ServiceAccountRead, PermissionScopeKind.Tenant),
      [PolicyNames.RequireServiceAccountRotateCredentials] = new(PermissionNames.ServiceAccountRotateCredentials, PermissionScopeKind.Tenant),
      [PolicyNames.RequireServiceAccountWrite] = new(PermissionNames.ServiceAccountWrite, PermissionScopeKind.Tenant),
      [PolicyNames.RequireTagsWrite] = new(PermissionNames.TenantTagsWrite, PermissionScopeKind.Tenant),
      [PolicyNames.RequireTenantSettingsRead] = new(PermissionNames.TenantSettingsRead, PermissionScopeKind.Tenant),
      [PolicyNames.RequireTenantSettingsWrite] = new(PermissionNames.TenantSettingsWrite, PermissionScopeKind.Tenant),
      [PolicyNames.RequireTenantUsersDelete] = new(PermissionNames.TenantUsersDelete, PermissionScopeKind.Tenant),
      [PolicyNames.RequireTenantUsersWrite] = new(PermissionNames.TenantUsersWrite, PermissionScopeKind.Tenant),
      [PolicyNames.RequireUserGroupAssignUsers] = new(PermissionNames.UserGroupAssignUsers, PermissionScopeKind.UserGroup),
      [PolicyNames.RequireUserGroupsRead] = new(PermissionNames.TenantUserGroupsRead, PermissionScopeKind.Tenant),
      [PolicyNames.RequireUserGroupsWrite] = new(PermissionNames.TenantUserGroupsWrite, PermissionScopeKind.Tenant),
      [PolicyNames.RequireUsersRead] = new(PermissionNames.TenantUsersRead, PermissionScopeKind.Tenant),
    };
  public static IReadOnlyDictionary<string, string> PolicyToPermission { get; } =
    Definitions.ToDictionary(x => x.Key, x => x.Value.PermissionName);
}
