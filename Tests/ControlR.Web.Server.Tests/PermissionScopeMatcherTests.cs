using ControlR.Web.Server.Authz.Permissions;
using ControlR.Web.Server.Services.Authorization;
using ControlR.Web.Server.Services.Authorization.PermissionRules;

namespace ControlR.Web.Server.Tests;

public class PermissionScopeMatcherTests
{
  [Fact]
  public void UserGroupRule_MatchesOnlyUserGroupDescriptor()
  {
    var scopeId = Guid.NewGuid();
    var rule = Rule(PermissionScopeKind.UserGroup, scopeId);

    Assert.True(PermissionScopeMatcher.Matches(rule, new ResourceDescriptor(PermissionScopeKind.UserGroup, scopeId, scopeId)));
    Assert.False(PermissionScopeMatcher.Matches(rule, new ResourceDescriptor(PermissionScopeKind.Device, scopeId, scopeId, scopeId, [scopeId])));
    Assert.False(PermissionScopeMatcher.Matches(rule, new ResourceDescriptor(PermissionScopeKind.CustomerTenant, scopeId, scopeId)));
  }

  // The scope-breadth table ranks UserGroup narrower than CustomerTenant and DeviceGroup. That
  // reordering is only decision-neutral because those kinds never land in the same matched set
  // for one resource. If the matcher ever let them co-match, the tie-break could silently
  // reorder deny resolution with no compile error and no failing test. This theory pins that.
  [Theory]
  [InlineData(PermissionScopeKind.Unknown)]
  [InlineData(PermissionScopeKind.Server)]
  [InlineData(PermissionScopeKind.Tenant)]
  [InlineData(PermissionScopeKind.CustomerTenant)]
  [InlineData(PermissionScopeKind.DeviceGroup)]
  [InlineData(PermissionScopeKind.Device)]
  [InlineData(PermissionScopeKind.UserGroup)]
  public void UserGroupRule_NeverCoMatchesCustomerTenantOrDeviceGroup(PermissionScopeKind resourceKind)
  {
    var scopeId = Guid.NewGuid();
    var tenantId = Guid.NewGuid();

    // Populate every id field so a co-match would fire if the kinds ever became reachable
    // together, instead of passing only because the descriptor was left sparse.
    var resource = new ResourceDescriptor(
      resourceKind,
      scopeId,
      tenantId,
      scopeId,
      [scopeId]);

    var userGroupMatch = PermissionScopeMatcher.Matches(Rule(PermissionScopeKind.UserGroup, scopeId), resource);
    var customerTenantMatch = PermissionScopeMatcher.Matches(Rule(PermissionScopeKind.CustomerTenant, scopeId), resource);
    var deviceGroupMatch = PermissionScopeMatcher.Matches(Rule(PermissionScopeKind.DeviceGroup, scopeId), resource);

    Assert.False(userGroupMatch && customerTenantMatch);
    Assert.False(userGroupMatch && deviceGroupMatch);
  }

  private static PermissionRule Rule(PermissionScopeKind scopeKind, Guid? scopeId) =>
    new(
      PermissionNames.UserGroupAssignUsers,
      PermissionEffect.Allow,
      scopeKind,
      scopeId,
      scopeId,
      RuleSource.Direct,
      SourcePriority.Direct);
}
