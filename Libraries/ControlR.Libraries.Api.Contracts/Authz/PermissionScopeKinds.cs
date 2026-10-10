namespace ControlR.Libraries.Api.Contracts.Authz;

public static class PermissionScopeKinds
{
  /// <summary>
  /// The breadth rank of a scope kind, where lower is narrower. Single source of this ordering in
  /// the codebase: used to pick the default scope for presets and the UI, and to break ties among
  /// rules that match one resource so the narrower rule wins.
  /// <see cref="PermissionScopeKind.UserGroup"/> ties with <see cref="PermissionScopeKind.DeviceGroup"/>
  /// because the two are orthogonal and never compete for one resource.
  /// </summary>
  /// <exception cref="ArgumentOutOfRangeException">
  /// Thrown for <see cref="PermissionScopeKind.Unknown"/> and any other value outside the six
  /// resource scope kinds. No legal rule reaches it. The scope matcher returns no match for a
  /// non-resource scope kind, and the assignment write path rejects such a kind before a rule can
  /// be stored.
  /// </exception>
  public static int GetBreadth(PermissionScopeKind scopeKind) => scopeKind switch
  {
    PermissionScopeKind.Device => 0,
    PermissionScopeKind.DeviceGroup => 1,
    PermissionScopeKind.UserGroup => 1,
    PermissionScopeKind.CustomerTenant => 2,
    PermissionScopeKind.Tenant => 3,
    PermissionScopeKind.Server => 4,
    _ => throw new ArgumentOutOfRangeException(
      nameof(scopeKind), scopeKind, "Not a legal resource scope kind.")
  };

  /// <summary>
  /// Returns the broadest scope kind from the set, or <see langword="null"/> when empty.
  /// </summary>
  public static PermissionScopeKind? GetBroadestLegalScope(
    IReadOnlyCollection<PermissionScopeKind> scopeKinds)
  {
    if (scopeKinds.Count == 0)
    {
      return null;
    }

    return scopeKinds.MaxBy(GetBreadth);
  }

  /// <summary>
  /// Returns the broadest scope kind from the set that stays within a tenant boundary, i.e.
  /// excludes <see cref="PermissionScopeKind.Server"/>. Used for tenant-facing UI default scope
  /// selection so a tenant admin is never pre-seeded a server-scoped (cross-tenant) grant. Falls
  /// back to the overall broadest legal scope when the permission is server-only.
  /// </summary>
  public static PermissionScopeKind? GetBroadestTenantLegalScope(
    IReadOnlyCollection<PermissionScopeKind> scopeKinds)
  {
    var tenantKinds = scopeKinds
      .Where(static kind => kind != PermissionScopeKind.Server)
      .ToList();

    if (tenantKinds.Count == 0)
    {
      return GetBroadestLegalScope(scopeKinds);
    }

    return tenantKinds.MaxBy(GetBreadth);
  }
}
