using ControlR.Web.Server.Authz.Permissions;
using ControlR.Web.Server.Data;
using ControlR.Web.Server.Data.Entities;
using ControlR.Web.Server.Services.Authorization;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace ControlR.Web.Server.Tests;

public class ClientPolicyGrantTests(ITestOutputHelper testOutput)
{
  private readonly ITestOutputHelper _testOutput = testOutput;

  [Fact]
  public async Task GetGrantedPolicies_DeviceScopedAllowDoesNotCreateGlobalGrant()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    await SeedFirstUser(testApp, tenant.Id);
    var user = await testApp.App.Services.CreateTestUser(tenant.Id);
    var device = await testApp.App.Services.CreateTestDevice(tenant.Id);

    await SeedAssignment(testApp, new PermissionAssignment
    {
      PrincipalKind = PermissionPrincipalKind.User,
      PrincipalId = user.Id,
      PermissionName = PermissionNames.DeviceTagsWrite,
      Effect = PermissionEffect.Allow,
      ScopeKind = PermissionScopeKind.Device,
      ScopeId = device.Id,
      OwningTenantId = tenant.Id,
      IsEnabled = true
    });

    var evaluator = GetEvaluator(testApp);
    var principal = CreateUserPrincipal(user.Id, tenant.Id);

    var result = await evaluator.GetGrantedPolicies(principal, TestContext.Current.CancellationToken);

    // DeviceTagsWrite maps to RequireTagsWrite only at tenant scope; a device-scoped allow must
    // not surface as a global client grant for tag management.
    Assert.DoesNotContain(PolicyNames.RequireTagsWrite, result);
  }

  [Fact]
  public async Task GetGrantedPolicies_NormalUserGetsNoUnrelatedPolicies()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    await SeedFirstUser(testApp, tenant.Id);
    var user = await testApp.App.Services.CreateTestUser(tenant.Id);

    var evaluator = GetEvaluator(testApp);
    var principal = CreateUserPrincipal(user.Id, tenant.Id);

    var result = await evaluator.GetGrantedPolicies(principal, TestContext.Current.CancellationToken);

    Assert.DoesNotContain(PolicyNames.RequireServerSettingsWrite, result);
  }

  [Fact]
  public async Task GetGrantedPolicies_ServerAdminGrantsServerPolicies()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    await SeedFirstUser(testApp, tenant.Id);
    var user = await testApp.App.Services.CreateTestUser(tenant.Id, presets: PermissionPresets.ServerAdministrator);

    var evaluator = GetEvaluator(testApp);
    var principal = CreateUserPrincipal(user.Id, tenant.Id);

    var result = await evaluator.GetGrantedPolicies(principal, TestContext.Current.CancellationToken);

    Assert.Contains(PolicyNames.RequireServerSettingsWrite, result);
    Assert.Contains(PolicyNames.RequireServerTelemetryRead, result);
  }

  [Fact]
  public async Task GetGrantedPolicies_TenantAdminGrantsTenantPolicies()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    await SeedFirstUser(testApp, tenant.Id);
    var user = await testApp.App.Services.CreateTestUser(tenant.Id, presets: PermissionPresets.TenantAdministrator);

    var evaluator = GetEvaluator(testApp);
    var principal = CreateUserPrincipal(user.Id, tenant.Id);

    var result = await evaluator.GetGrantedPolicies(principal, TestContext.Current.CancellationToken);

    Assert.Contains(PolicyNames.RequireCustomersRead, result);
    Assert.Contains(PolicyNames.RequireCustomersWrite, result);
  }

  [Fact]
  public async Task GetGrantedPolicies_TenantDenyRemovesMappedPolicy()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    await SeedFirstUser(testApp, tenant.Id);
    var user = await testApp.App.Services.CreateTestUser(tenant.Id);

    await SeedAssignment(testApp, new PermissionAssignment
    {
      PrincipalKind = PermissionPrincipalKind.User,
      PrincipalId = user.Id,
      PermissionName = PermissionNames.TenantCustomersRead,
      Effect = PermissionEffect.Allow,
      ScopeKind = PermissionScopeKind.Tenant,
      ScopeId = tenant.Id,
      OwningTenantId = tenant.Id,
      IsEnabled = true
    });

    await SeedAssignment(testApp, new PermissionAssignment
    {
      PrincipalKind = PermissionPrincipalKind.User,
      PrincipalId = user.Id,
      PermissionName = PermissionNames.TenantCustomersRead,
      Effect = PermissionEffect.Deny,
      ScopeKind = PermissionScopeKind.Tenant,
      ScopeId = tenant.Id,
      OwningTenantId = tenant.Id,
      IsEnabled = true
    });

    var evaluator = GetEvaluator(testApp);
    var principal = CreateUserPrincipal(user.Id, tenant.Id);

    var result = await evaluator.GetGrantedPolicies(principal, TestContext.Current.CancellationToken);

    Assert.DoesNotContain(PolicyNames.RequireCustomersRead, result);
  }

  private static PrincipalDescriptor CreateUserPrincipal(Guid userId, Guid tenantId) =>
    new(PrincipalType.User, userId, tenantId, AuthMethod: "cookie");

  private static IPermissionEvaluator GetEvaluator(TestApp testApp) =>
    testApp.App.Services.GetRequiredService<IPermissionEvaluator>();

  private static async Task SeedAssignment(TestApp testApp, PermissionAssignment assignment)
  {
    using var scope = testApp.App.Services.CreateScope();
    await using var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    db.PermissionAssignments.Add(assignment);
    await db.SaveChangesAsync();
  }

  private static async Task SeedFirstUser(TestApp testApp, Guid tenantId)
  {
    await testApp.App.Services.CreateTestUser(
      tenantId,
      email: $"seed-{Guid.NewGuid():N}@t.local");
  }
}
