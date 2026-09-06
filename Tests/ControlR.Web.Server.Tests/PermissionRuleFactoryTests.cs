using ControlR.Libraries.Api.Contracts.Enums;
using ControlR.Web.Server.Authz.Permissions;
using ControlR.Web.Server.Data.Entities;
using ControlR.Web.Server.Services.Authorization.PermissionRules;

namespace ControlR.Web.Server.Tests;

public class PermissionRuleFactoryTests
{
  [Fact]
  public void CreateDirectRules_FiltersEnabledAndTenantOwned()
  {
    var tenantId = Guid.NewGuid();
    var otherTenant = Guid.NewGuid();
    var assignments = new[]
    {
      CreateAssignment(tenantId),
      CreateAssignment(null),
      CreateAssignment(otherTenant),
      CreateAssignment(tenantId, isEnabled: false)
    };

    var rules = PermissionRuleFactory.CreateDirectRules(assignments, tenantId);

    Assert.Equal(2, rules.Count);
    Assert.All(rules, rule => Assert.Equal(RuleSource.Direct, rule.Source));
    Assert.All(rules, rule => Assert.Equal(SourcePriority.Direct, rule.Priority));
  }

  [Fact]
  public void CreateDirectRules_ServerScopeDeny_ForTenantBoundPrincipal_IsKept()
  {
    var tenantId = Guid.NewGuid();
    var assignments = new[]
    {
      CreateScopedAssignment(tenantId, PermissionNames.DeviceRead, PermissionScopeKind.Server, null,
        PermissionEffect.Deny)
    };

    var rules = PermissionRuleFactory.CreateDirectRules(assignments, tenantId);

    var rule = Assert.Single(rules);
    Assert.Equal(PermissionEffect.Deny, rule.Effect);
  }

  [Fact]
  public void CreateDirectRules_ServerScopeOnServerOnlyPermission_ForTenantBoundPrincipal_IsKept()
  {
    var tenantId = Guid.NewGuid();
    var assignments = new[]
    {
      CreateScopedAssignment(tenantId, PermissionNames.ServerAlertsRead, PermissionScopeKind.Server, null)
    };

    var rules = PermissionRuleFactory.CreateDirectRules(assignments, tenantId);

    Assert.Single(rules);
  }

  [Fact]
  public void CreateDirectRules_ServerScopeOnTenantAddressablePermission_ForTenantBoundPrincipal_IsDropped()
  {
    var tenantId = Guid.NewGuid();
    var assignments = new[]
    {
      CreateScopedAssignment(tenantId, PermissionNames.DeviceRead, PermissionScopeKind.Server, null),
      CreateScopedAssignment(tenantId, PermissionNames.DeviceRead, PermissionScopeKind.Tenant, tenantId)
    };

    var rules = PermissionRuleFactory.CreateDirectRules(assignments, tenantId);

    var rule = Assert.Single(rules);
    Assert.Equal(PermissionScopeKind.Tenant, rule.ScopeKind);
  }

  [Fact]
  public void CreateDirectRules_ServerScopeOnTenantAddressablePermission_ForTenantLessPrincipal_IsKept()
  {
    var assignments = new[]
    {
      CreateScopedAssignment(null, PermissionNames.DeviceRead, PermissionScopeKind.Server, null)
    };

    var rules = PermissionRuleFactory.CreateDirectRules(assignments, tenantId: null);

    var rule = Assert.Single(rules);
    Assert.Equal(PermissionScopeKind.Server, rule.ScopeKind);
  }

  [Fact]
  public void CreateGroupRules_UsesUserGroupSourceAndPriority()
  {
    var tenantId = Guid.NewGuid();
    var assignments = new[] { CreateAssignment(tenantId) };

    var rules = PermissionRuleFactory.CreateGroupRules(assignments, tenantId);

    var rule = Assert.Single(rules);
    Assert.Equal(RuleSource.UserGroup, rule.Source);
    Assert.Equal(SourcePriority.UserGroup, rule.Priority);
  }

  private static PermissionAssignment CreateAssignment(
    Guid? owningTenantId,
    bool isEnabled = true) =>
    CreateScopedAssignment(
      owningTenantId,
      PermissionNames.DeviceRead,
      PermissionScopeKind.Tenant,
      Guid.NewGuid(),
      PermissionEffect.Allow,
      isEnabled);

  private static PermissionAssignment CreateScopedAssignment(
    Guid? owningTenantId,
    string permissionName,
    PermissionScopeKind scopeKind,
    Guid? scopeId,
    PermissionEffect effect = PermissionEffect.Allow,
    bool isEnabled = true) =>
    PermissionAssignment.CreateGrant(
      PermissionPrincipalKind.User,
      Guid.NewGuid(),
      permissionName,
      scopeKind,
      scopeId,
      owningTenantId,
      new PrincipalDescriptor(PrincipalType.User, Guid.NewGuid(), owningTenantId, "test"),
      effect,
      isEnabled: isEnabled);
}
