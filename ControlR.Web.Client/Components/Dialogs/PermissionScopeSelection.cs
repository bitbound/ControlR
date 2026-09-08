using ControlR.Libraries.Api.Contracts.Authz;
using ControlR.Libraries.Api.Contracts.Enums;

namespace ControlR.Web.Client.Components.Dialogs;

/// <summary>
/// Pure scope-selection rules for <see cref="PermissionAssignmentDialog"/>, mirroring the
/// server write gates. Separated from the component so the rules are unit-testable without
/// a component harness. The server remains authoritative.
/// </summary>
internal static class PermissionScopeSelection
{

  /// <summary>
  /// Scope kinds shown in the picker: the permission's whitelist, minus Server when the
  /// caller lacks server permission-management authority or the principal/effect make
  /// Server illegal.
  /// </summary>
  internal static IReadOnlyList<PermissionScopeKind> AvailableScopeKinds(
    IReadOnlyList<PermissionScopeKind> allowedScopeKinds,
    PermissionEffect effect,
    bool principalAllowsServerScope,
    bool canManageServerScope)
  {
    if (canManageServerScope && ServerScopeAllowed(allowedScopeKinds, effect, principalAllowsServerScope))
    {
      return allowedScopeKinds;
    }

    return [.. allowedScopeKinds.Where(static kind => kind != PermissionScopeKind.Server)];
  }

  /// <summary>
  /// Broadest scope for the picker's default selection, always an element of
  /// <see cref="AvailableScopeKinds"/>.
  /// </summary>
  internal static PermissionScopeKind BroadestSelectable(
    IReadOnlyList<PermissionScopeKind> allowedScopeKinds,
    PermissionEffect effect,
    bool principalAllowsServerScope,
    bool canManageServerScope)
  {
    var kinds = AvailableScopeKinds(allowedScopeKinds, effect, principalAllowsServerScope, canManageServerScope);

    // Server is selectable for a deny but never pre-selected, so a tenant-bound principal is
    // never seeded a server-wide row by default. Server-only permissions are exempt: Server is
    // their only legal scope, and stripping it would default onto an unoffered, unaccepted
    // scope.
    if (!principalAllowsServerScope && allowedScopeKinds.Contains(PermissionScopeKind.Tenant))
    {
      kinds = [.. kinds.Where(static kind => kind != PermissionScopeKind.Server)];
    }

    return PermissionScopeKinds.GetBroadestLegalScope(kinds) ?? PermissionScopeKind.Tenant;
  }

  /// <summary>
  /// True when the Server scope is legal for this permission, target principal, and effect.
  /// Denies are exempt from the reach rule. Server-only permissions must stay assignable to
  /// users.
  /// </summary>
  internal static bool ServerScopeAllowed(
    IReadOnlyList<PermissionScopeKind> allowedScopeKinds,
    PermissionEffect effect,
    bool principalAllowsServerScope) =>
    principalAllowsServerScope
    || effect == PermissionEffect.Deny
    || !allowedScopeKinds.Contains(PermissionScopeKind.Tenant);
}
