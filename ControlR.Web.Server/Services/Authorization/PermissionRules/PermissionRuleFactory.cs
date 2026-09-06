using ControlR.Web.Server.Authz.Permissions;

namespace ControlR.Web.Server.Services.Authorization.PermissionRules;

/// <summary>
/// Builds <see cref="PermissionRule"/> instances from <see cref="PermissionAssignment"/> rows,
/// applying the shared enabled-row and tenant-ownership filtering used by the evaluation context
/// loader, the credential rule paths, and the self-protection path so they cannot drift apart.
/// </summary>
public static class PermissionRuleFactory
{
  public static IReadOnlyList<PermissionRule> CreateCredentialRules(
    IEnumerable<PermissionAssignment> assignments,
    Guid? tenantId,
    RuleSource source,
    SourcePriority priority) =>
    CreateRules(assignments, tenantId, source, priority);

  public static IReadOnlyList<PermissionRule> CreateDirectRules(
    IEnumerable<PermissionAssignment> assignments,
    Guid? tenantId) =>
    CreateRules(assignments, tenantId, RuleSource.Direct, SourcePriority.Direct);

  public static IReadOnlyList<PermissionRule> CreateGroupRules(
    IEnumerable<PermissionAssignment> assignments,
    Guid? tenantId) =>
    CreateRules(assignments, tenantId, RuleSource.UserGroup, SourcePriority.UserGroup);

  private static IReadOnlyList<PermissionRule> CreateRules(
    IEnumerable<PermissionAssignment> assignments,
    Guid? tenantId,
    RuleSource source,
    SourcePriority priority) =>
    [.. assignments
      .Where(assignment => IsApplicable(assignment, tenantId))
      .Select(assignment => PermissionRule.Create(assignment, source, priority))];

  private static bool IsApplicable(PermissionAssignment assignment, Guid? tenantId) =>
    assignment.IsEnabled &&
    IsWithinPrincipalTenant(assignment, tenantId) &&
    !IsOverreachingServerScope(assignment, tenantId);

  // A tenant-bound principal cannot legitimately hold a Server-scope allow of a tenant-addressable
  // permission, so the row is dropped. Denies are kept: dropping one would widen access.
  private static bool IsOverreachingServerScope(
    PermissionAssignment assignment,
    Guid? tenantId) =>
    tenantId is not null &&
    assignment.ScopeKind == PermissionScopeKind.Server &&
    assignment.Effect == PermissionEffect.Allow &&
    PermissionCatalog.AllowsTenantScope(assignment.PermissionName);

  private static bool IsWithinPrincipalTenant(
    PermissionAssignment assignment,
    Guid? tenantId) =>
    tenantId is null ||
    assignment.OwningTenantId is null ||
    assignment.OwningTenantId == tenantId;
}
