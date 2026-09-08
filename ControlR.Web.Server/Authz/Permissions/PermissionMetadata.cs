using System.Collections.Immutable;

namespace ControlR.Web.Server.Authz.Permissions;

public sealed record PermissionMetadata(
  string Name,
  string DisplayName,
  string Description,
  ImmutableArray<PermissionScopeKind> AllowedScopeKinds,
  bool SelfRemovable = true)
{
  /// <summary>
  /// <para>
  ///   True when the permission is addressable within a single tenant. Granting such a permission at
  ///   <see cref="PermissionScopeKind.Server"/> scope reaches resources in every tenant, so it is only
  ///   meaningful for a principal with no tenant binding.
  /// </para>
  /// <para>
  ///   When this is false, the permission is a server-administration permission. Those are not
  ///   addressable in a tenant at all, so Server is already their only allowed scope.
  /// </para>
  /// </summary>
  public bool AllowsTenantScope => AllowedScopeKinds.Contains(PermissionScopeKind.Tenant);
}
