using ControlR.Web.Server.Authz.Permissions;
using ControlR.Web.Server.Authz.Policies;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace ControlR.Web.Server.Tests;

public class AuthorizationRuntimePolicyTests(ITestOutputHelper testOutput)
{
  private readonly ITestOutputHelper _testOutput = testOutput;

  [Fact]
  public async Task RegisteredPolicies_ContainExpectedPermissionRequirements()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var provider = testApp.Services.GetRequiredService<IAuthorizationPolicyProvider>();

    foreach (var (policyName, definition) in PermissionPolicies.Definitions)
    {
      var policy = await provider.GetPolicyAsync(policyName);
      Assert.NotNull(policy);
      var requirement = Assert.Single(policy.Requirements.OfType<PermissionRequirement>());
      Assert.Equal(definition.PermissionNames, requirement.PermissionNames);
      Assert.Equal(definition.ResourceScopeKind, requirement.Resource.Kind);
    }

    foreach (var (policyName, permissionName) in DeviceResourcePolicies.PolicyToPermission)
    {
      var policy = await provider.GetPolicyAsync(policyName);
      Assert.NotNull(policy);
      var requirement = Assert.Single(policy.Requirements.OfType<PermissionRequirement>());
      Assert.Equal([permissionName], requirement.PermissionNames);
      Assert.Equal(PermissionScopeKind.Device, requirement.Resource.Kind);
    }
  }
}
