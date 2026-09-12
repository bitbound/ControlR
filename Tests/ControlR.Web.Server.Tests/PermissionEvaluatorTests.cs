using ControlR.Web.Server.Authn;
using ControlR.Web.Server.Authz.Permissions;
using ControlR.Web.Server.Data;
using ControlR.Web.Server.Data.Entities;
using ControlR.Web.Server.Services.Authorization;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ControlR.Web.Server.Tests;

public class PermissionEvaluatorTests(ITestOutputHelper testOutput)
{
  private readonly ITestOutputHelper _testOutput = testOutput;

  [Fact]
  public async Task CustomerScopeAssignment_CoversDeviceInCustomer()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    var user = await testApp.App.Services.CreateTestUser(tenant.Id);
    var device = await testApp.App.Services.CreateTestDevice(tenant.Id);
    var customerId = Guid.NewGuid();

    await SeedAssignment(testApp, new PermissionAssignment
    {
      PrincipalKind = PermissionPrincipalKind.User,
      PrincipalId = user.Id,
      PermissionName = PermissionNames.DeviceRead,
      Effect = PermissionEffect.Allow,
      ScopeKind = PermissionScopeKind.CustomerTenant,
      ScopeId = customerId,
      OwningTenantId = tenant.Id,
      IsEnabled = true
    });

    var evaluator = GetEvaluator(testApp);
    var principal = CreateUserPrincipal(user.Id, tenant.Id);
    var resource = new ResourceDescriptor(PermissionScopeKind.Device, device.Id, tenant.Id, customerId);

    var result = await evaluator.Evaluate(principal, PermissionNames.DeviceRead, resource, TestContext.Current.CancellationToken);

    Assert.True(result.Allowed);
  }

  [Fact]
  public async Task CustomerScopeAssignment_DeniesDeviceInOtherCustomer()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    var user = await testApp.App.Services.CreateTestUser(tenant.Id);
    var device = await testApp.App.Services.CreateTestDevice(tenant.Id);
    var customerId = Guid.NewGuid();

    await SeedAssignment(testApp, new PermissionAssignment
    {
      PrincipalKind = PermissionPrincipalKind.User,
      PrincipalId = user.Id,
      PermissionName = PermissionNames.DeviceRead,
      Effect = PermissionEffect.Allow,
      ScopeKind = PermissionScopeKind.CustomerTenant,
      ScopeId = customerId,
      OwningTenantId = tenant.Id,
      IsEnabled = true
    });

    var evaluator = GetEvaluator(testApp);
    var principal = CreateUserPrincipal(user.Id, tenant.Id);
    var resource = new ResourceDescriptor(PermissionScopeKind.Device, device.Id, tenant.Id, Guid.NewGuid());

    var result = await evaluator.Evaluate(principal, PermissionNames.DeviceRead, resource, TestContext.Current.CancellationToken);

    Assert.False(result.Allowed);
    Assert.Contains("default deny", result.DenialReason);
  }

  [Fact]
  public async Task CustomerScopeAssignment_DeniesUnassignedDevice()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    var user = await testApp.App.Services.CreateTestUser(tenant.Id);
    var device = await testApp.App.Services.CreateTestDevice(tenant.Id);
    var customerId = Guid.NewGuid();

    await SeedAssignment(testApp, new PermissionAssignment
    {
      PrincipalKind = PermissionPrincipalKind.User,
      PrincipalId = user.Id,
      PermissionName = PermissionNames.DeviceRead,
      Effect = PermissionEffect.Allow,
      ScopeKind = PermissionScopeKind.CustomerTenant,
      ScopeId = customerId,
      OwningTenantId = tenant.Id,
      IsEnabled = true
    });

    var evaluator = GetEvaluator(testApp);
    var principal = CreateUserPrincipal(user.Id, tenant.Id);
    var resource = new ResourceDescriptor(PermissionScopeKind.Device, device.Id, tenant.Id);

    var result = await evaluator.Evaluate(principal, PermissionNames.DeviceRead, resource, TestContext.Current.CancellationToken);

    Assert.False(result.Allowed);
  }

  [Fact]
  public async Task CustomerScopeDeny_OverridesTenantAllow()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    var user = await testApp.App.Services.CreateTestUser(tenant.Id);
    var device = await testApp.App.Services.CreateTestDevice(tenant.Id);
    var customerId = Guid.NewGuid();

    await SeedAssignment(testApp, new PermissionAssignment
    {
      PrincipalKind = PermissionPrincipalKind.User,
      PrincipalId = user.Id,
      PermissionName = PermissionNames.DeviceRead,
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
      PermissionName = PermissionNames.DeviceRead,
      Effect = PermissionEffect.Deny,
      ScopeKind = PermissionScopeKind.CustomerTenant,
      ScopeId = customerId,
      OwningTenantId = tenant.Id,
      IsEnabled = true
    });

    var evaluator = GetEvaluator(testApp);
    var principal = CreateUserPrincipal(user.Id, tenant.Id);
    var resource = new ResourceDescriptor(PermissionScopeKind.Device, device.Id, tenant.Id, customerId);

    var result = await evaluator.Evaluate(principal, PermissionNames.DeviceRead, resource, TestContext.Current.CancellationToken);

    Assert.False(result.Allowed);
    Assert.Contains("Explicit deny", result.DenialReason);
  }

  [Fact]
  public async Task DefaultDeny_WhenNoRules_Denies()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    var user = await testApp.App.Services.CreateTestUser(tenant.Id);
    var device = await testApp.App.Services.CreateTestDevice(tenant.Id);

    var evaluator = GetEvaluator(testApp);
    var principal = CreateUserPrincipal(user.Id, tenant.Id);
    var resource = new ResourceDescriptor(PermissionScopeKind.Device, device.Id, tenant.Id);

    var result = await evaluator.Evaluate(principal, PermissionNames.DeviceRead, resource, TestContext.Current.CancellationToken);

    Assert.False(result.Allowed);
    Assert.Contains("default deny", result.DenialReason);
  }

  [Fact]
  public async Task DenyOverridesAllow_WhenBothExist_Denies()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    var user = await testApp.App.Services.CreateTestUser(tenant.Id);
    var device = await testApp.App.Services.CreateTestDevice(tenant.Id);

    await SeedAssignment(testApp, new PermissionAssignment
    {
      PrincipalKind = PermissionPrincipalKind.User,
      PrincipalId = user.Id,
      PermissionName = PermissionNames.DeviceDelete,
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
      PermissionName = PermissionNames.DeviceDelete,
      Effect = PermissionEffect.Deny,
      ScopeKind = PermissionScopeKind.Device,
      ScopeId = device.Id,
      OwningTenantId = tenant.Id,
      IsEnabled = true
    });

    var evaluator = GetEvaluator(testApp);
    var principal = CreateUserPrincipal(user.Id, tenant.Id);
    var resource = new ResourceDescriptor(PermissionScopeKind.Device, device.Id, tenant.Id);

    var result = await evaluator.Evaluate(principal, PermissionNames.DeviceDelete, resource, TestContext.Current.CancellationToken);

    Assert.False(result.Allowed);
    Assert.Contains("Explicit deny", result.DenialReason);
  }

  [Fact]
  public async Task DenyViaUserGroup_OverridesDirectAllow()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    var user = await testApp.App.Services.CreateTestUser(tenant.Id);
    var device = await testApp.App.Services.CreateTestDevice(tenant.Id);

    var groupId = Guid.NewGuid();
    await SeedUserGroup(testApp, groupId, tenant.Id, user.Id);

    await SeedAssignment(testApp, new PermissionAssignment
    {
      PrincipalKind = PermissionPrincipalKind.User,
      PrincipalId = user.Id,
      PermissionName = PermissionNames.DeviceDelete,
      Effect = PermissionEffect.Allow,
      ScopeKind = PermissionScopeKind.Tenant,
      ScopeId = tenant.Id,
      OwningTenantId = tenant.Id,
      IsEnabled = true
    });

    await SeedAssignment(testApp, new PermissionAssignment
    {
      PrincipalKind = PermissionPrincipalKind.UserGroup,
      PrincipalId = groupId,
      PermissionName = PermissionNames.DeviceDelete,
      Effect = PermissionEffect.Deny,
      ScopeKind = PermissionScopeKind.Device,
      ScopeId = device.Id,
      OwningTenantId = tenant.Id,
      IsEnabled = true
    });

    var evaluator = GetEvaluator(testApp);
    var principal = CreateUserPrincipal(user.Id, tenant.Id);
    var resource = new ResourceDescriptor(PermissionScopeKind.Device, device.Id, tenant.Id);

    var result = await evaluator.Evaluate(principal, PermissionNames.DeviceDelete, resource, TestContext.Current.CancellationToken);

    Assert.False(result.Allowed);
    Assert.Contains("Explicit deny", result.DenialReason);
  }

  [Fact]
  public async Task DeviceGroupScopeAssignment_CoversDevice()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    var user = await testApp.App.Services.CreateTestUser(tenant.Id);
    var device = await testApp.App.Services.CreateTestDevice(tenant.Id);
    var deviceGroupId = Guid.NewGuid();

    await SeedAssignment(testApp, new PermissionAssignment
    {
      PrincipalKind = PermissionPrincipalKind.User,
      PrincipalId = user.Id,
      PermissionName = PermissionNames.DeviceRead,
      Effect = PermissionEffect.Allow,
      ScopeKind = PermissionScopeKind.DeviceGroup,
      ScopeId = deviceGroupId,
      OwningTenantId = tenant.Id,
      IsEnabled = true
    });

    var evaluator = GetEvaluator(testApp);
    var principal = CreateUserPrincipal(user.Id, tenant.Id);
    var resource = new ResourceDescriptor(PermissionScopeKind.Device, device.Id, tenant.Id, DeviceGroupIds: [deviceGroupId]);

    var result = await evaluator.Evaluate(principal, PermissionNames.DeviceRead, resource, TestContext.Current.CancellationToken);

    Assert.True(result.Allowed);
    Assert.Equal("Direct", result.MatchedRuleSource);
  }

  [Fact]
  public async Task DeviceGroupScopeAssignment_DeniesDeviceNotInGroup()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    var user = await testApp.App.Services.CreateTestUser(tenant.Id);
    var device = await testApp.App.Services.CreateTestDevice(tenant.Id);
    var deviceGroupId = Guid.NewGuid();

    await SeedAssignment(testApp, new PermissionAssignment
    {
      PrincipalKind = PermissionPrincipalKind.User,
      PrincipalId = user.Id,
      PermissionName = PermissionNames.DeviceRead,
      Effect = PermissionEffect.Allow,
      ScopeKind = PermissionScopeKind.DeviceGroup,
      ScopeId = deviceGroupId,
      OwningTenantId = tenant.Id,
      IsEnabled = true
    });

    var evaluator = GetEvaluator(testApp);
    var principal = CreateUserPrincipal(user.Id, tenant.Id);
    var resource = new ResourceDescriptor(PermissionScopeKind.Device, device.Id, tenant.Id, DeviceGroupIds: [Guid.NewGuid()]);

    var result = await evaluator.Evaluate(principal, PermissionNames.DeviceRead, resource, TestContext.Current.CancellationToken);

    Assert.False(result.Allowed);
    Assert.Contains("default deny", result.DenialReason);
  }

  [Fact]
  public async Task DeviceGroupScopeAssignment_DeniesDeviceWithUnknownMembership()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    var user = await testApp.App.Services.CreateTestUser(tenant.Id);
    var device = await testApp.App.Services.CreateTestDevice(tenant.Id);
    var deviceGroupId = Guid.NewGuid();

    await SeedAssignment(testApp, new PermissionAssignment
    {
      PrincipalKind = PermissionPrincipalKind.User,
      PrincipalId = user.Id,
      PermissionName = PermissionNames.DeviceRead,
      Effect = PermissionEffect.Allow,
      ScopeKind = PermissionScopeKind.DeviceGroup,
      ScopeId = deviceGroupId,
      OwningTenantId = tenant.Id,
      IsEnabled = true
    });

    var evaluator = GetEvaluator(testApp);
    var principal = CreateUserPrincipal(user.Id, tenant.Id);
    var resource = new ResourceDescriptor(PermissionScopeKind.Device, device.Id, tenant.Id);

    var result = await evaluator.Evaluate(principal, PermissionNames.DeviceRead, resource, TestContext.Current.CancellationToken);

    Assert.False(result.Allowed);
    Assert.Contains("default deny", result.DenialReason);
  }

  [Fact]
  public async Task DeviceGroupScopeDeny_OverridesTenantAllow()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    var user = await testApp.App.Services.CreateTestUser(tenant.Id);
    var device = await testApp.App.Services.CreateTestDevice(tenant.Id);
    var deviceGroupId = Guid.NewGuid();

    await SeedAssignment(testApp, new PermissionAssignment
    {
      PrincipalKind = PermissionPrincipalKind.User,
      PrincipalId = user.Id,
      PermissionName = PermissionNames.DeviceRead,
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
      PermissionName = PermissionNames.DeviceRead,
      Effect = PermissionEffect.Deny,
      ScopeKind = PermissionScopeKind.DeviceGroup,
      ScopeId = deviceGroupId,
      OwningTenantId = tenant.Id,
      IsEnabled = true
    });

    var evaluator = GetEvaluator(testApp);
    var principal = CreateUserPrincipal(user.Id, tenant.Id);
    var resource = new ResourceDescriptor(PermissionScopeKind.Device, device.Id, tenant.Id, DeviceGroupIds: [deviceGroupId]);

    var result = await evaluator.Evaluate(principal, PermissionNames.DeviceRead, resource, TestContext.Current.CancellationToken);

    Assert.False(result.Allowed);
    Assert.Contains("Explicit deny", result.DenialReason);
  }

  [Fact]
  public async Task DirectAllow_WhenAssignmentExists_Allows()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    var user = await testApp.App.Services.CreateTestUser(tenant.Id);
    var device = await testApp.App.Services.CreateTestDevice(tenant.Id);

    await SeedAssignment(testApp, new PermissionAssignment
    {
      PrincipalKind = PermissionPrincipalKind.User,
      PrincipalId = user.Id,
      PermissionName = PermissionNames.DeviceRead,
      Effect = PermissionEffect.Allow,
      ScopeKind = PermissionScopeKind.Device,
      ScopeId = device.Id,
      OwningTenantId = tenant.Id,
      IsEnabled = true
    });

    var evaluator = GetEvaluator(testApp);
    var principal = CreateUserPrincipal(user.Id, tenant.Id);
    var resource = new ResourceDescriptor(PermissionScopeKind.Device, device.Id, tenant.Id);

    var result = await evaluator.Evaluate(principal, PermissionNames.DeviceRead, resource, TestContext.Current.CancellationToken);

    Assert.True(result.Allowed);
    Assert.Equal("Direct", result.MatchedRuleSource);
  }

  [Fact]
  public async Task DisabledAssignment_IsIgnored()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    var user = await testApp.App.Services.CreateTestUser(tenant.Id);
    var device = await testApp.App.Services.CreateTestDevice(tenant.Id);

    await SeedAssignment(testApp, new PermissionAssignment
    {
      PrincipalKind = PermissionPrincipalKind.User,
      PrincipalId = user.Id,
      PermissionName = PermissionNames.DeviceRead,
      Effect = PermissionEffect.Allow,
      ScopeKind = PermissionScopeKind.Device,
      ScopeId = device.Id,
      OwningTenantId = tenant.Id,
      IsEnabled = false
    });

    var evaluator = GetEvaluator(testApp);
    var principal = CreateUserPrincipal(user.Id, tenant.Id);
    var resource = new ResourceDescriptor(PermissionScopeKind.Device, device.Id, tenant.Id);

    var result = await evaluator.Evaluate(principal, PermissionNames.DeviceRead, resource, TestContext.Current.CancellationToken);

    Assert.False(result.Allowed);
    Assert.Contains("default deny", result.DenialReason);
  }

  [Fact]
  public async Task EvaluateBatch_PreservesOrderAndCardinalityAcrossResources()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    var user = await testApp.App.Services.CreateTestUser(tenant.Id);
    var deviceA = await testApp.App.Services.CreateTestDevice(tenant.Id);
    var deviceB = await testApp.App.Services.CreateTestDevice(tenant.Id);
    await SeedAssignment(testApp, PermissionAssignment.CreateGrant(
      PermissionPrincipalKind.User,
      user.Id,
      PermissionNames.DeviceRead,
      PermissionScopeKind.Device,
      deviceA.Id,
      tenant.Id,
      new PrincipalDescriptor(PrincipalType.User, user.Id, tenant.Id, "test")));

    var evaluator = GetEvaluator(testApp);
    var principal = CreateUserPrincipal(user.Id, tenant.Id);
    var requests = new[]
    {
      new PermissionEvaluationRequest(
        PermissionNames.DeviceRead,
        new ResourceDescriptor(PermissionScopeKind.Device, deviceB.Id, tenant.Id)),
      new PermissionEvaluationRequest(
        PermissionNames.DeviceRead,
        new ResourceDescriptor(PermissionScopeKind.Device, deviceA.Id, tenant.Id)),
      new PermissionEvaluationRequest(
        PermissionNames.DeviceRead,
        new ResourceDescriptor(PermissionScopeKind.Device, deviceB.Id, tenant.Id))
    };

    var results = await evaluator.EvaluateBatch(
      principal,
      requests,
      TestContext.Current.CancellationToken);

    Assert.Equal(3, results.Count);
    Assert.False(results[requests[0]].Allowed);
    Assert.True(results[requests[1]].Allowed);
    Assert.False(results[requests[2]].Allowed);
  }

  [Fact]
  public async Task EvaluateMany_MatchesRepeatedPointChecksAndDeduplicatesNames()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    var user = await testApp.App.Services.CreateTestUser(tenant.Id);
    var device = await testApp.App.Services.CreateTestDevice(tenant.Id);

    await SeedAssignment(testApp, PermissionAssignment.CreateGrant(
      PermissionPrincipalKind.User,
      user.Id,
      PermissionNames.DeviceRead,
      PermissionScopeKind.Tenant,
      tenant.Id,
      tenant.Id,
      new PrincipalDescriptor(PrincipalType.User, user.Id, tenant.Id, "test")));
    await SeedAssignment(testApp, PermissionAssignment.CreateGrant(
      PermissionPrincipalKind.User,
      user.Id,
      PermissionNames.DeviceDelete,
      PermissionScopeKind.Device,
      device.Id,
      tenant.Id,
      new PrincipalDescriptor(PrincipalType.User, user.Id, tenant.Id, "test"),
      PermissionEffect.Deny));

    var evaluator = GetEvaluator(testApp);
    var principal = CreateUserPrincipal(user.Id, tenant.Id);
    var resource = new ResourceDescriptor(PermissionScopeKind.Device, device.Id, tenant.Id);
    var names = new[]
    {
      PermissionNames.DeviceRead,
      PermissionNames.DeviceDelete,
      PermissionNames.DeviceRead
    };

    var bulk = await evaluator.EvaluateMany(
      principal,
      names,
      resource,
      TestContext.Current.CancellationToken);
    var read = await evaluator.Evaluate(
      principal,
      PermissionNames.DeviceRead,
      resource,
      TestContext.Current.CancellationToken);
    var delete = await evaluator.Evaluate(
      principal,
      PermissionNames.DeviceDelete,
      resource,
      TestContext.Current.CancellationToken);

    Assert.Equal(2, bulk.Count);
    Assert.Equal(read.Allowed, bulk[PermissionNames.DeviceRead].Allowed);
    Assert.Equal(delete.Allowed, bulk[PermissionNames.DeviceDelete].Allowed);
  }

  [Fact]
  public async Task EvaluateRules_IllegalScopeKind_FailsClosed()
  {
    // A persisted row with an illegal (permission, scopeKind) combination must not authorize,
    // even though it bypasses manager write validation (e.g. direct DB writes). The evaluator
    // checks the catalog's allowed scope kinds as defense-in-depth.
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    var user = await testApp.App.Services.CreateTestUser(tenant.Id);
    var device = await testApp.App.Services.CreateTestDevice(tenant.Id);

    await SeedAssignment(testApp, new PermissionAssignment
    {
      PrincipalKind = PermissionPrincipalKind.User,
      PrincipalId = user.Id,
      PermissionName = PermissionNames.InstallerKeyRead,
      Effect = PermissionEffect.Allow,
      ScopeKind = PermissionScopeKind.Device,
      ScopeId = device.Id,
      OwningTenantId = tenant.Id,
      IsEnabled = true
    });

    var evaluator = GetEvaluator(testApp);
    var principal = CreateUserPrincipal(user.Id, tenant.Id);
    var deviceResource = new ResourceDescriptor(PermissionScopeKind.Device, device.Id, tenant.Id);

    var result = await evaluator.Evaluate(principal, PermissionNames.InstallerKeyRead, deviceResource, TestContext.Current.CancellationToken);

    Assert.False(result.Allowed);
  }

  [Fact]
  public async Task Evaluate_UnknownPermissionWithStaleAllow_Denies()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    var user = await testApp.App.Services.CreateTestUser(tenant.Id);
    const string unknownPermission = "device.stale.unknown";
    await SeedAssignment(testApp, PermissionAssignment.CreateGrant(
      PermissionPrincipalKind.User,
      user.Id,
      unknownPermission,
      PermissionScopeKind.Tenant,
      tenant.Id,
      tenant.Id,
      new PrincipalDescriptor(PrincipalType.User, user.Id, tenant.Id, "test")));

    var evaluator = GetEvaluator(testApp);
    var result = await evaluator.Evaluate(
      CreateUserPrincipal(user.Id, tenant.Id),
      unknownPermission,
      new ResourceDescriptor(PermissionScopeKind.Tenant, tenant.Id, tenant.Id),
      TestContext.Current.CancellationToken);

    Assert.False(result.Allowed);
    Assert.Contains("Unknown permission", result.DenialReason);
  }

  [Fact]
  public async Task LogonTokenGrants_ForRecipientWithNoPermissions_Allows()
  {
    // The recipient (e.g., an external/transient user) has no permissions of their own.
    // The logon token's device grants are authoritative and still allow access.
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    var user = await testApp.App.Services.CreateTestUser(tenant.Id);
    var device = await testApp.App.Services.CreateTestDevice(tenant.Id);
    var tokenId = Guid.NewGuid();

    await SeedAssignment(testApp, new PermissionAssignment
    {
      PrincipalKind = PermissionPrincipalKind.LogonToken,
      PrincipalId = tokenId,
      PermissionName = PermissionNames.DeviceRemoteControlConnect,
      Effect = PermissionEffect.Allow,
      ScopeKind = PermissionScopeKind.Device,
      ScopeId = device.Id,
      OwningTenantId = tenant.Id,
      IsEnabled = true
    });

    var evaluator = GetEvaluator(testApp);
    var principal = CreateUserPrincipal(user.Id, tenant.Id,
      credentialId: tokenId,
      credentialType: CredentialType.LogonToken,
      deviceScopeId: device.Id);
    var resource = new ResourceDescriptor(PermissionScopeKind.Device, device.Id, tenant.Id);

    var result = await evaluator.Evaluate(principal, PermissionNames.DeviceRemoteControlConnect, resource, TestContext.Current.CancellationToken);

    Assert.True(result.Allowed);
    Assert.Equal("LogonTokenGrant", result.MatchedRuleSource);
  }

  [Fact]
  public async Task LogonTokenGrants_WhenDeviceScopeMismatch_Denies()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    var user = await testApp.App.Services.CreateTestUser(tenant.Id);
    var deviceA = await testApp.App.Services.CreateTestDevice(tenant.Id);
    var deviceB = await testApp.App.Services.CreateTestDevice(tenant.Id);
    var tokenId = Guid.NewGuid();

    await SeedAssignment(testApp, new PermissionAssignment
    {
      PrincipalKind = PermissionPrincipalKind.User,
      PrincipalId = user.Id,
      PermissionName = PermissionNames.DeviceRemoteControlConnect,
      Effect = PermissionEffect.Allow,
      ScopeKind = PermissionScopeKind.Tenant,
      ScopeId = tenant.Id,
      OwningTenantId = tenant.Id,
      IsEnabled = true
    });

    await SeedAssignment(testApp, new PermissionAssignment
    {
      PrincipalKind = PermissionPrincipalKind.LogonToken,
      PrincipalId = tokenId,
      PermissionName = PermissionNames.DeviceRemoteControlConnect,
      Effect = PermissionEffect.Allow,
      ScopeKind = PermissionScopeKind.Device,
      ScopeId = deviceA.Id,
      OwningTenantId = tenant.Id,
      IsEnabled = true
    });

    var evaluator = GetEvaluator(testApp);
    var principal = CreateUserPrincipal(user.Id, tenant.Id,
      credentialId: tokenId,
      credentialType: CredentialType.LogonToken,
      deviceScopeId: deviceB.Id);
    var resource = new ResourceDescriptor(PermissionScopeKind.Device, deviceB.Id, tenant.Id);

    var result = await evaluator.Evaluate(principal, PermissionNames.DeviceRemoteControlConnect, resource, TestContext.Current.CancellationToken);

    Assert.False(result.Allowed);
    Assert.Contains("device scope", result.DenialReason);
  }

  [Fact]
  public async Task LogonTokenGrants_WhenMatchingAllow_Allows()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    var user = await testApp.App.Services.CreateTestUser(tenant.Id);
    var device = await testApp.App.Services.CreateTestDevice(tenant.Id);
    var tokenId = Guid.NewGuid();

    await SeedAssignment(testApp, new PermissionAssignment
    {
      PrincipalKind = PermissionPrincipalKind.User,
      PrincipalId = user.Id,
      PermissionName = PermissionNames.DeviceRemoteControlConnect,
      Effect = PermissionEffect.Allow,
      ScopeKind = PermissionScopeKind.Tenant,
      ScopeId = tenant.Id,
      OwningTenantId = tenant.Id,
      IsEnabled = true
    });

    await SeedAssignment(testApp, new PermissionAssignment
    {
      PrincipalKind = PermissionPrincipalKind.LogonToken,
      PrincipalId = tokenId,
      PermissionName = PermissionNames.DeviceRemoteControlConnect,
      Effect = PermissionEffect.Allow,
      ScopeKind = PermissionScopeKind.Device,
      ScopeId = device.Id,
      OwningTenantId = tenant.Id,
      IsEnabled = true
    });

    var evaluator = GetEvaluator(testApp);
    var principal = CreateUserPrincipal(user.Id, tenant.Id,
      credentialId: tokenId,
      credentialType: CredentialType.LogonToken,
      deviceScopeId: device.Id);
    var resource = new ResourceDescriptor(PermissionScopeKind.Device, device.Id, tenant.Id);

    var result = await evaluator.Evaluate(principal, PermissionNames.DeviceRemoteControlConnect, resource, TestContext.Current.CancellationToken);

    Assert.True(result.Allowed);
    Assert.Equal("LogonTokenGrant", result.MatchedRuleSource);
  }

  [Fact]
  public async Task LogonTokenGrants_WhenOutsideUserPermissions_Allows()
  {
    // Logon tokens carry their own device grants authoritatively and are NOT bounded by
    // the recipient's permissions (the recipient may be an external user with none). The
    // grants are set by the device.logon-token.create holder at creation time.
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    var user = await testApp.App.Services.CreateTestUser(tenant.Id);
    var device = await testApp.App.Services.CreateTestDevice(tenant.Id);
    var tokenId = Guid.NewGuid();

    await SeedAssignment(testApp, new PermissionAssignment
    {
      PrincipalKind = PermissionPrincipalKind.User,
      PrincipalId = user.Id,
      PermissionName = PermissionNames.DeviceRead,
      Effect = PermissionEffect.Allow,
      ScopeKind = PermissionScopeKind.Tenant,
      ScopeId = tenant.Id,
      OwningTenantId = tenant.Id,
      IsEnabled = true
    });

    await SeedAssignment(testApp, new PermissionAssignment
    {
      PrincipalKind = PermissionPrincipalKind.LogonToken,
      PrincipalId = tokenId,
      PermissionName = PermissionNames.DeviceDelete,
      Effect = PermissionEffect.Allow,
      ScopeKind = PermissionScopeKind.Device,
      ScopeId = device.Id,
      OwningTenantId = tenant.Id,
      IsEnabled = true
    });

    var evaluator = GetEvaluator(testApp);
    var principal = CreateUserPrincipal(user.Id, tenant.Id,
      credentialId: tokenId,
      credentialType: CredentialType.LogonToken,
      deviceScopeId: device.Id);
    var resource = new ResourceDescriptor(PermissionScopeKind.Device, device.Id, tenant.Id);

    var result = await evaluator.Evaluate(principal, PermissionNames.DeviceDelete, resource, TestContext.Current.CancellationToken);

    Assert.True(result.Allowed);
    Assert.Equal("LogonTokenGrant", result.MatchedRuleSource);
  }

  [Fact]
  public async Task LogonTokenGrants_WhenTokenHasDenyAtDeviceScope_Denies()
  {
    // The logon-token path rebuilds rules solely from the token's grants; a deny among those
    // grants must still override allows (deny-overrides-allow applies to every source).
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    var user = await testApp.App.Services.CreateTestUser(tenant.Id);
    var device = await testApp.App.Services.CreateTestDevice(tenant.Id);
    var tokenId = Guid.NewGuid();

    await SeedAssignment(testApp, new PermissionAssignment
    {
      PrincipalKind = PermissionPrincipalKind.User,
      PrincipalId = user.Id,
      PermissionName = PermissionNames.DeviceRemoteControlConnect,
      Effect = PermissionEffect.Allow,
      ScopeKind = PermissionScopeKind.Tenant,
      ScopeId = tenant.Id,
      OwningTenantId = tenant.Id,
      IsEnabled = true
    });

    await SeedAssignment(testApp, new PermissionAssignment
    {
      PrincipalKind = PermissionPrincipalKind.LogonToken,
      PrincipalId = tokenId,
      PermissionName = PermissionNames.DeviceRemoteControlConnect,
      Effect = PermissionEffect.Allow,
      ScopeKind = PermissionScopeKind.Device,
      ScopeId = device.Id,
      OwningTenantId = tenant.Id,
      IsEnabled = true
    });

    await SeedAssignment(testApp, new PermissionAssignment
    {
      PrincipalKind = PermissionPrincipalKind.LogonToken,
      PrincipalId = tokenId,
      PermissionName = PermissionNames.DeviceRemoteControlConnect,
      Effect = PermissionEffect.Deny,
      ScopeKind = PermissionScopeKind.Device,
      ScopeId = device.Id,
      OwningTenantId = tenant.Id,
      IsEnabled = true
    });

    var evaluator = GetEvaluator(testApp);
    var principal = CreateUserPrincipal(user.Id, tenant.Id,
      credentialId: tokenId,
      credentialType: CredentialType.LogonToken,
      deviceScopeId: device.Id);
    var resource = new ResourceDescriptor(PermissionScopeKind.Device, device.Id, tenant.Id);

    var result = await evaluator.Evaluate(principal, PermissionNames.DeviceRemoteControlConnect, resource, TestContext.Current.CancellationToken);

    Assert.False(result.Allowed);
  }

  [Fact]
  public async Task LogonTokenGrants_WhenZeroRows_Denies()
  {
    // A credential with no scope rows grants nothing.
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    var user = await testApp.App.Services.CreateTestUser(tenant.Id);
    var device = await testApp.App.Services.CreateTestDevice(tenant.Id);
    var tokenId = Guid.NewGuid();

    await SeedAssignment(testApp, new PermissionAssignment
    {
      PrincipalKind = PermissionPrincipalKind.User,
      PrincipalId = user.Id,
      PermissionName = PermissionNames.DeviceRemoteControlConnect,
      Effect = PermissionEffect.Allow,
      ScopeKind = PermissionScopeKind.Tenant,
      ScopeId = tenant.Id,
      OwningTenantId = tenant.Id,
      IsEnabled = true
    });

    var evaluator = GetEvaluator(testApp);
    var principal = CreateUserPrincipal(user.Id, tenant.Id,
      credentialId: tokenId,
      credentialType: CredentialType.LogonToken,
      deviceScopeId: device.Id);
    var resource = new ResourceDescriptor(PermissionScopeKind.Device, device.Id, tenant.Id);

    var result = await evaluator.Evaluate(principal, PermissionNames.DeviceRemoteControlConnect, resource, TestContext.Current.CancellationToken);

    Assert.False(result.Allowed);
  }

  [Fact]
  public async Task PatInheritOwner_DeletedTokenRow_DeniedInsteadOfInheriting()
  {
    // A dangling credential (PersonalAccessToken row deleted) must NOT degrade to
    // the InheritOwner path. The nullable projection in PermissionEvaluationContextLoader
    // yields null when the token row is missing, so it should be denied rather than
    // inheriting owner permissions.
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var (user, tenant, device, tokenId) = await SeedPatScenario(testApp, PersonalAccessTokenPermissionMode.InheritOwner);

    var evaluator = GetEvaluator(testApp);
    var principal = CreateUserPrincipal(user.Id, tenant.Id, tokenId, CredentialType.PersonalAccessToken);
    var resource = new ResourceDescriptor(PermissionScopeKind.Device, device.Id, tenant.Id);

    // Sanity: inherit-owner allows owner permissions while token row exists.
    var before = await evaluator.Evaluate(principal, PermissionNames.DeviceRead, resource, TestContext.Current.CancellationToken);
    Assert.True(before.Allowed);

    // Delete the token row itself.
    using (var scope = testApp.App.Services.CreateScope())
    {
      await using var db = scope.ServiceProvider.GetRequiredService<AppDb>();
      var token = await db.PersonalAccessTokens
        .IgnoreQueryFilters()
        .FirstOrDefaultAsync(t => t.Id == tokenId, TestContext.Current.CancellationToken);
      Assert.NotNull(token);
      db.PersonalAccessTokens.Remove(token);
      await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    // After deletion, the principal must NOT inherit owner permissions.
    var after = await evaluator.Evaluate(principal, PermissionNames.DeviceRead, resource, TestContext.Current.CancellationToken);
    Assert.False(after.Allowed,
      "A dangling credential (deleted token row) must not fall back to the InheritOwner path.");
  }

  [Fact]
  public async Task PatInheritOwner_NoRows_AllowsOwnerPermissions()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var (user, tenant, device, tokenId) = await SeedPatScenario(testApp, PersonalAccessTokenPermissionMode.InheritOwner);

    var evaluator = GetEvaluator(testApp);
    var principal = CreateUserPrincipal(user.Id, tenant.Id, tokenId, CredentialType.PersonalAccessToken);
    var resource = new ResourceDescriptor(PermissionScopeKind.Device, device.Id, tenant.Id);

    var result = await evaluator.Evaluate(principal, PermissionNames.DeviceRead, resource, TestContext.Current.CancellationToken);

    Assert.True(result.Allowed);
  }

  [Fact]
  public async Task PatRestricted_AfterDeletingAllRows_DeniesInsteadOfEscalating()
  {
    // A restricted PAT whose rows are all removed (e.g., by scope trimming) must deny
    // rather than fall back to the owner's full permissions.
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var (user, tenant, device, tokenId) = await SeedPatScenario(testApp, PersonalAccessTokenPermissionMode.Restricted);

    await SeedAssignment(testApp, new PermissionAssignment
    {
      PrincipalKind = PermissionPrincipalKind.PersonalAccessToken,
      PrincipalId = tokenId,
      PermissionName = PermissionNames.DeviceRead,
      Effect = PermissionEffect.Allow,
      ScopeKind = PermissionScopeKind.Device,
      ScopeId = device.Id,
      OwningTenantId = tenant.Id,
      IsEnabled = true
    });

    var evaluator = GetEvaluator(testApp);
    var principal = CreateUserPrincipal(user.Id, tenant.Id, tokenId, CredentialType.PersonalAccessToken);
    var resource = new ResourceDescriptor(PermissionScopeKind.Device, device.Id, tenant.Id);

    var before = await evaluator.Evaluate(principal, PermissionNames.DeviceRead, resource, TestContext.Current.CancellationToken);
    Assert.True(before.Allowed);

    using (var scope = testApp.App.Services.CreateScope())
    {
      await using var db = scope.ServiceProvider.GetRequiredService<AppDb>();
      var rows = await db.PermissionAssignments
        .IgnoreQueryFilters()
        .Where(x => x.PrincipalKind == PermissionPrincipalKind.PersonalAccessToken && x.PrincipalId == tokenId)
        .ToListAsync(TestContext.Current.CancellationToken);
      db.PermissionAssignments.RemoveRange(rows);
      await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    var after = await evaluator.Evaluate(principal, PermissionNames.DeviceRead, resource, TestContext.Current.CancellationToken);
    Assert.False(after.Allowed);
  }

  [Fact]
  public async Task PatRestricted_NoRows_DeniesAllIncludingOwnerPermissions()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var (user, tenant, device, tokenId) = await SeedPatScenario(testApp, PersonalAccessTokenPermissionMode.Restricted);

    var evaluator = GetEvaluator(testApp);
    var principal = CreateUserPrincipal(user.Id, tenant.Id, tokenId, CredentialType.PersonalAccessToken);
    var resource = new ResourceDescriptor(PermissionScopeKind.Device, device.Id, tenant.Id);

    var result = await evaluator.Evaluate(principal, PermissionNames.DeviceRead, resource, TestContext.Current.CancellationToken);

    Assert.False(result.Allowed);
  }

  [Fact]
  public async Task PatRestricted_WithRows_StillBoundedByOwnerPermissions()
  {
    // A restricted PAT is granted DeviceRead via its own scope row. It must be allowed for
    // that permission but denied for one the owner holds and the PAT does not.
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var (user, tenant, device, tokenId) = await SeedPatScenario(testApp, PersonalAccessTokenPermissionMode.Restricted);

    // Owner additionally holds DeviceDelete; the restricted PAT should not.
    await SeedAssignment(testApp, new PermissionAssignment
    {
      PrincipalKind = PermissionPrincipalKind.User,
      PrincipalId = user.Id,
      PermissionName = PermissionNames.DeviceDelete,
      Effect = PermissionEffect.Allow,
      ScopeKind = PermissionScopeKind.Tenant,
      ScopeId = tenant.Id,
      OwningTenantId = tenant.Id,
      IsEnabled = true
    });

    await SeedAssignment(testApp, new PermissionAssignment
    {
      PrincipalKind = PermissionPrincipalKind.PersonalAccessToken,
      PrincipalId = tokenId,
      PermissionName = PermissionNames.DeviceRead,
      Effect = PermissionEffect.Allow,
      ScopeKind = PermissionScopeKind.Device,
      ScopeId = device.Id,
      OwningTenantId = tenant.Id,
      IsEnabled = true
    });

    var evaluator = GetEvaluator(testApp);
    var principal = CreateUserPrincipal(user.Id, tenant.Id, tokenId, CredentialType.PersonalAccessToken);
    var resource = new ResourceDescriptor(PermissionScopeKind.Device, device.Id, tenant.Id);

    var read = await evaluator.Evaluate(principal, PermissionNames.DeviceRead, resource, TestContext.Current.CancellationToken);
    Assert.True(read.Allowed);

    var delete = await evaluator.Evaluate(principal, PermissionNames.DeviceDelete, resource, TestContext.Current.CancellationToken);
    Assert.False(delete.Allowed);
  }

  [Fact]
  public async Task PatScopes_BoundedByUserGroupPermissions()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    var user = await testApp.App.Services.CreateTestUser(tenant.Id);
    var device = await testApp.App.Services.CreateTestDevice(tenant.Id);
    var patId = Guid.NewGuid();

    var groupId = Guid.NewGuid();
    await SeedUserGroup(testApp, groupId, tenant.Id, user.Id);

    await SeedAssignment(testApp, new PermissionAssignment
    {
      PrincipalKind = PermissionPrincipalKind.UserGroup,
      PrincipalId = groupId,
      PermissionName = PermissionNames.DeviceRead,
      Effect = PermissionEffect.Allow,
      ScopeKind = PermissionScopeKind.Tenant,
      ScopeId = tenant.Id,
      OwningTenantId = tenant.Id,
      IsEnabled = true
    });

    await SeedAssignment(testApp, new PermissionAssignment
    {
      PrincipalKind = PermissionPrincipalKind.PersonalAccessToken,
      PrincipalId = patId,
      PermissionName = PermissionNames.DeviceRead,
      Effect = PermissionEffect.Allow,
      ScopeKind = PermissionScopeKind.Device,
      ScopeId = device.Id,
      OwningTenantId = tenant.Id,
      IsEnabled = true
    });

    var evaluator = GetEvaluator(testApp);
    var principal = CreateUserPrincipal(user.Id, tenant.Id,
      credentialId: patId,
      credentialType: CredentialType.PersonalAccessToken);
    var resource = new ResourceDescriptor(PermissionScopeKind.Device, device.Id, tenant.Id);

    var result = await evaluator.Evaluate(principal, PermissionNames.DeviceRead, resource, TestContext.Current.CancellationToken);

    Assert.True(result.Allowed);
    Assert.Equal("PatGrant", result.MatchedRuleSource);
  }

  [Fact]
  public async Task PatScopes_OwnerLacksPermission_BoundingGuardDenies()
  {
    // Exercise the owner-bounding guard. A restricted PAT with a scope row granting
    // DeviceDelete, but the owner lacks any DeviceDelete assignment, must be denied by
    // the bounding check (not merely because PAT rules are empty).
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var (user, tenant, device, patId) = await SeedPatScenario(testApp, PersonalAccessTokenPermissionMode.Restricted);

    // Seed a PAT scope row granting DeviceDelete. The user holds NO DeviceDelete.
    await SeedAssignment(testApp, new PermissionAssignment
    {
      PrincipalKind = PermissionPrincipalKind.PersonalAccessToken,
      PrincipalId = patId,
      PermissionName = PermissionNames.DeviceDelete,
      Effect = PermissionEffect.Allow,
      ScopeKind = PermissionScopeKind.Device,
      ScopeId = device.Id,
      OwningTenantId = tenant.Id,
      IsEnabled = true
    });

    var evaluator = GetEvaluator(testApp);
    var principal = CreateUserPrincipal(user.Id, tenant.Id, patId, CredentialType.PersonalAccessToken);
    var resource = new ResourceDescriptor(PermissionScopeKind.Device, device.Id, tenant.Id);

    var deleteResult = await evaluator.Evaluate(principal, PermissionNames.DeviceDelete, resource, TestContext.Current.CancellationToken);

    Assert.False(deleteResult.Allowed,
      "A PAT scope row granting a permission the owner lacks must be denied by the bounding guard.");
  }

  [Fact]
  public async Task PatScopes_WhenMatchingAllow_Allows()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    var user = await testApp.App.Services.CreateTestUser(tenant.Id);
    var device = await testApp.App.Services.CreateTestDevice(tenant.Id);
    var patId = Guid.NewGuid();

    await SeedAssignment(testApp, new PermissionAssignment
    {
      PrincipalKind = PermissionPrincipalKind.User,
      PrincipalId = user.Id,
      PermissionName = PermissionNames.DeviceRead,
      Effect = PermissionEffect.Allow,
      ScopeKind = PermissionScopeKind.Tenant,
      ScopeId = tenant.Id,
      OwningTenantId = tenant.Id,
      IsEnabled = true
    });

    await SeedAssignment(testApp, new PermissionAssignment
    {
      PrincipalKind = PermissionPrincipalKind.PersonalAccessToken,
      PrincipalId = patId,
      PermissionName = PermissionNames.DeviceRead,
      Effect = PermissionEffect.Allow,
      ScopeKind = PermissionScopeKind.Device,
      ScopeId = device.Id,
      OwningTenantId = tenant.Id,
      IsEnabled = true
    });

    var evaluator = GetEvaluator(testApp);
    var principal = CreateUserPrincipal(user.Id, tenant.Id,
      credentialId: patId,
      credentialType: CredentialType.PersonalAccessToken);
    var resource = new ResourceDescriptor(PermissionScopeKind.Device, device.Id, tenant.Id);

    var result = await evaluator.Evaluate(principal, PermissionNames.DeviceRead, resource, TestContext.Current.CancellationToken);

    Assert.True(result.Allowed);
    Assert.Equal("PatGrant", result.MatchedRuleSource);
  }

  [Fact]
  public async Task PatScopes_WhenOutsideUserPermissions_Denies()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    var user = await testApp.App.Services.CreateTestUser(tenant.Id);
    var device = await testApp.App.Services.CreateTestDevice(tenant.Id);
    var patId = Guid.NewGuid();

    await SeedAssignment(testApp, new PermissionAssignment
    {
      PrincipalKind = PermissionPrincipalKind.PersonalAccessToken,
      PrincipalId = patId,
      PermissionName = PermissionNames.DeviceDelete,
      Effect = PermissionEffect.Allow,
      ScopeKind = PermissionScopeKind.Device,
      ScopeId = device.Id,
      OwningTenantId = tenant.Id,
      IsEnabled = true
    });

    var evaluator = GetEvaluator(testApp);
    var principal = CreateUserPrincipal(user.Id, tenant.Id,
      credentialId: patId,
      credentialType: CredentialType.PersonalAccessToken);
    var resource = new ResourceDescriptor(PermissionScopeKind.Device, device.Id, tenant.Id);

    var result = await evaluator.Evaluate(principal, PermissionNames.DeviceDelete, resource, TestContext.Current.CancellationToken);

    Assert.False(result.Allowed);
    Assert.Contains("outside the user's effective permissions", result.DenialReason);
  }

  [Fact]
  public async Task PatScopes_WhenRowScopeBeyondUserScope_Denies()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    var user = await testApp.App.Services.CreateTestUser(tenant.Id);
    var deviceA = await testApp.App.Services.CreateTestDevice(tenant.Id);
    var deviceB = await testApp.App.Services.CreateTestDevice(tenant.Id);
    var patId = Guid.NewGuid();

    await SeedAssignment(testApp, new PermissionAssignment
    {
      PrincipalKind = PermissionPrincipalKind.User,
      PrincipalId = user.Id,
      PermissionName = PermissionNames.DeviceRead,
      Effect = PermissionEffect.Allow,
      ScopeKind = PermissionScopeKind.Device,
      ScopeId = deviceA.Id,
      OwningTenantId = tenant.Id,
      IsEnabled = true
    });

    await SeedAssignment(testApp, new PermissionAssignment
    {
      PrincipalKind = PermissionPrincipalKind.PersonalAccessToken,
      PrincipalId = patId,
      PermissionName = PermissionNames.DeviceRead,
      Effect = PermissionEffect.Allow,
      ScopeKind = PermissionScopeKind.Device,
      ScopeId = deviceB.Id,
      OwningTenantId = tenant.Id,
      IsEnabled = true
    });

    var evaluator = GetEvaluator(testApp);
    var principal = CreateUserPrincipal(user.Id, tenant.Id,
      credentialId: patId,
      credentialType: CredentialType.PersonalAccessToken);
    var resource = new ResourceDescriptor(PermissionScopeKind.Device, deviceB.Id, tenant.Id);

    var result = await evaluator.Evaluate(principal, PermissionNames.DeviceRead, resource, TestContext.Current.CancellationToken);

    Assert.False(result.Allowed);
  }

  [Fact]
  public async Task PatScopes_WhenUserDeniedAtDeviceScope_DeniesDespiteMembershipCoverage()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    var user = await testApp.App.Services.CreateTestUser(tenant.Id);
    var device = await testApp.App.Services.CreateTestDevice(tenant.Id);
    var patId = Guid.NewGuid();

    await SeedAssignment(testApp, new PermissionAssignment
    {
      PrincipalKind = PermissionPrincipalKind.User,
      PrincipalId = user.Id,
      PermissionName = PermissionNames.DeviceRead,
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
      PermissionName = PermissionNames.DeviceRead,
      Effect = PermissionEffect.Deny,
      ScopeKind = PermissionScopeKind.Device,
      ScopeId = device.Id,
      OwningTenantId = tenant.Id,
      IsEnabled = true
    });

    await SeedAssignment(testApp, new PermissionAssignment
    {
      PrincipalKind = PermissionPrincipalKind.PersonalAccessToken,
      PrincipalId = patId,
      PermissionName = PermissionNames.DeviceRead,
      Effect = PermissionEffect.Allow,
      ScopeKind = PermissionScopeKind.Device,
      ScopeId = device.Id,
      OwningTenantId = tenant.Id,
      IsEnabled = true
    });

    var evaluator = GetEvaluator(testApp);
    var principal = CreateUserPrincipal(user.Id, tenant.Id,
      credentialId: patId,
      credentialType: CredentialType.PersonalAccessToken);
    var resource = new ResourceDescriptor(PermissionScopeKind.Device, device.Id, tenant.Id);

    var result = await evaluator.Evaluate(principal, PermissionNames.DeviceRead, resource, TestContext.Current.CancellationToken);

    Assert.False(result.Allowed);
  }

  [Fact]
  public async Task PatScopes_WhenUserDeniedAtRowScope_Denies()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    var user = await testApp.App.Services.CreateTestUser(tenant.Id);
    var deviceB = await testApp.App.Services.CreateTestDevice(tenant.Id);
    var patId = Guid.NewGuid();

    await SeedAssignment(testApp, new PermissionAssignment
    {
      PrincipalKind = PermissionPrincipalKind.User,
      PrincipalId = user.Id,
      PermissionName = PermissionNames.DeviceRead,
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
      PermissionName = PermissionNames.DeviceRead,
      Effect = PermissionEffect.Deny,
      ScopeKind = PermissionScopeKind.Device,
      ScopeId = deviceB.Id,
      OwningTenantId = tenant.Id,
      IsEnabled = true
    });

    await SeedAssignment(testApp, new PermissionAssignment
    {
      PrincipalKind = PermissionPrincipalKind.PersonalAccessToken,
      PrincipalId = patId,
      PermissionName = PermissionNames.DeviceRead,
      Effect = PermissionEffect.Allow,
      ScopeKind = PermissionScopeKind.Device,
      ScopeId = deviceB.Id,
      OwningTenantId = tenant.Id,
      IsEnabled = true
    });

    var evaluator = GetEvaluator(testApp);
    var principal = CreateUserPrincipal(user.Id, tenant.Id,
      credentialId: patId,
      credentialType: CredentialType.PersonalAccessToken);
    var resource = new ResourceDescriptor(PermissionScopeKind.Device, deviceB.Id, tenant.Id);

    var result = await evaluator.Evaluate(principal, PermissionNames.DeviceRead, resource, TestContext.Current.CancellationToken);

    Assert.False(result.Allowed);
  }

  [Fact]
  public async Task PatScopes_WhenUserHasGroupAllow_CoversDeviceRowInGroup()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    var user = await testApp.App.Services.CreateTestUser(tenant.Id);
    var device = await testApp.App.Services.CreateTestDevice(tenant.Id);
    var patId = Guid.NewGuid();
    var groupId = Guid.NewGuid();

    using (var scope = testApp.App.Services.CreateScope())
    {
      await using var db = scope.ServiceProvider.GetRequiredService<AppDb>();
      db.DeviceGroups.Add(new DeviceGroup { Id = groupId, Name = $"group-{groupId:N}", TenantId = tenant.Id });
      db.DeviceGroupMembers.Add(new DeviceGroupMember { DeviceId = device.Id, DeviceGroupId = groupId });
      await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    await SeedAssignment(testApp, new PermissionAssignment
    {
      PrincipalKind = PermissionPrincipalKind.User,
      PrincipalId = user.Id,
      PermissionName = PermissionNames.DeviceRead,
      Effect = PermissionEffect.Allow,
      ScopeKind = PermissionScopeKind.DeviceGroup,
      ScopeId = groupId,
      OwningTenantId = tenant.Id,
      IsEnabled = true
    });

    await SeedAssignment(testApp, new PermissionAssignment
    {
      PrincipalKind = PermissionPrincipalKind.PersonalAccessToken,
      PrincipalId = patId,
      PermissionName = PermissionNames.DeviceRead,
      Effect = PermissionEffect.Allow,
      ScopeKind = PermissionScopeKind.Device,
      ScopeId = device.Id,
      OwningTenantId = tenant.Id,
      IsEnabled = true
    });

    var evaluator = GetEvaluator(testApp);
    var principal = CreateUserPrincipal(user.Id, tenant.Id,
      credentialId: patId,
      credentialType: CredentialType.PersonalAccessToken);
    var resource = new ResourceDescriptor(
      PermissionScopeKind.Device,
      device.Id,
      tenant.Id,
      DeviceGroupIds: [groupId]);

    var result = await evaluator.Evaluate(principal, PermissionNames.DeviceRead, resource, TestContext.Current.CancellationToken);

    Assert.True(result.Allowed);
    Assert.Equal("PatGrant", result.MatchedRuleSource);
  }

  [Fact]
  public async Task PatScopes_WhenZeroRows_AllowsServerTelemetryRead_WhenOwnerHasIt()
  {
    // An inherit-owner PAT with no explicit scope rows acts as its owning user, so it inherits
    // the user's server-level permissions (including server topic subscriptions).
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    var user = await testApp.App.Services.CreateTestUser(tenant.Id);
    var patId = Guid.NewGuid();

    await SeedPersonalAccessToken(testApp, user.Id, patId, PersonalAccessTokenPermissionMode.InheritOwner);

    await SeedAssignment(testApp, new PermissionAssignment
    {
      PrincipalKind = PermissionPrincipalKind.User,
      PrincipalId = user.Id,
      PermissionName = PermissionNames.ServerTelemetryRead,
      Effect = PermissionEffect.Allow,
      ScopeKind = PermissionScopeKind.Server,
      ScopeId = null,
      OwningTenantId = null,
      IsEnabled = true
    });

    var evaluator = GetEvaluator(testApp);
    var principal = CreateUserPrincipal(user.Id, tenant.Id,
      credentialId: patId,
      credentialType: CredentialType.PersonalAccessToken);
    var serverResource = new ResourceDescriptor(PermissionScopeKind.Server);

    var result = await evaluator.Evaluate(principal, PermissionNames.ServerTelemetryRead, serverResource, TestContext.Current.CancellationToken);

    Assert.True(result.Allowed);
  }

  [Fact]
  public async Task PatScopes_WhenZeroRows_InheritsUserPermissions()
  {
    // An inherit-owner PAT with no explicit scope rows acts as its owning user, inheriting the
    // user's full effective permissions.
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    var user = await testApp.App.Services.CreateTestUser(tenant.Id);
    var device = await testApp.App.Services.CreateTestDevice(tenant.Id);
    var patId = Guid.NewGuid();

    await SeedPersonalAccessToken(testApp, user.Id, patId, PersonalAccessTokenPermissionMode.InheritOwner);

    await SeedAssignment(testApp, new PermissionAssignment
    {
      PrincipalKind = PermissionPrincipalKind.User,
      PrincipalId = user.Id,
      PermissionName = PermissionNames.DeviceRead,
      Effect = PermissionEffect.Allow,
      ScopeKind = PermissionScopeKind.Tenant,
      ScopeId = tenant.Id,
      OwningTenantId = tenant.Id,
      IsEnabled = true
    });

    var evaluator = GetEvaluator(testApp);
    var principal = CreateUserPrincipal(user.Id, tenant.Id,
      credentialId: patId,
      credentialType: CredentialType.PersonalAccessToken);
    var resource = new ResourceDescriptor(PermissionScopeKind.Device, device.Id, tenant.Id);

    var result = await evaluator.Evaluate(principal, PermissionNames.DeviceRead, resource, TestContext.Current.CancellationToken);

    Assert.True(result.Allowed);
  }

  [Fact]
  public async Task PatScopes_WithDeviceRow_DeniesServerTelemetryRead_EvenWhenOwnerHasIt()
  {
    // The owning user holds ServerTelemetryRead at server scope, but the PAT has an explicit
    // device-scoped row. ViewerHub.JoinServerTopics relies on Evaluate (not the name-level
    // projection) so the scoped credential cannot subscribe to server topics.
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    var user = await testApp.App.Services.CreateTestUser(tenant.Id);
    var device = await testApp.App.Services.CreateTestDevice(tenant.Id);
    var patId = Guid.NewGuid();

    await SeedAssignment(testApp, new PermissionAssignment
    {
      PrincipalKind = PermissionPrincipalKind.User,
      PrincipalId = user.Id,
      PermissionName = PermissionNames.ServerTelemetryRead,
      Effect = PermissionEffect.Allow,
      ScopeKind = PermissionScopeKind.Server,
      ScopeId = null,
      OwningTenantId = null,
      IsEnabled = true
    });

    await SeedAssignment(testApp, new PermissionAssignment
    {
      PrincipalKind = PermissionPrincipalKind.User,
      PrincipalId = user.Id,
      PermissionName = PermissionNames.DeviceRead,
      Effect = PermissionEffect.Allow,
      ScopeKind = PermissionScopeKind.Tenant,
      ScopeId = tenant.Id,
      OwningTenantId = tenant.Id,
      IsEnabled = true
    });

    await SeedAssignment(testApp, new PermissionAssignment
    {
      PrincipalKind = PermissionPrincipalKind.PersonalAccessToken,
      PrincipalId = patId,
      PermissionName = PermissionNames.DeviceRead,
      Effect = PermissionEffect.Allow,
      ScopeKind = PermissionScopeKind.Device,
      ScopeId = device.Id,
      OwningTenantId = tenant.Id,
      IsEnabled = true
    });

    var evaluator = GetEvaluator(testApp);
    var principal = CreateUserPrincipal(user.Id, tenant.Id,
      credentialId: patId,
      credentialType: CredentialType.PersonalAccessToken);
    var serverResource = new ResourceDescriptor(PermissionScopeKind.Server);

    var result = await evaluator.Evaluate(principal, PermissionNames.ServerTelemetryRead, serverResource, TestContext.Current.CancellationToken);

    Assert.False(result.Allowed);
  }

  [Fact]
  public async Task ServerScopedAssignment_CoversAnyTenantResource()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenantA = await testApp.App.Services.CreateTestTenant();
    var tenantB = await testApp.App.Services.CreateTestTenant();
    var deviceA = await testApp.App.Services.CreateTestDevice(tenantA.Id);
    var deviceB = await testApp.App.Services.CreateTestDevice(tenantB.Id);
    var serviceAccountId = await SeedServerServiceAccount(testApp, ServiceAccountAccessMode.Restricted);

    await SeedAssignment(testApp, new PermissionAssignment
    {
      PrincipalKind = PermissionPrincipalKind.ServiceAccount,
      PrincipalId = serviceAccountId,
      PermissionName = PermissionNames.DeviceRead,
      Effect = PermissionEffect.Allow,
      ScopeKind = PermissionScopeKind.Server,
      ScopeId = null,
      OwningTenantId = null,
      IsEnabled = true
    });

    var evaluator = GetEvaluator(testApp);
    var principal = new PrincipalDescriptor(
      PrincipalType.ServerServiceAccount,
      serviceAccountId,
      TenantId: null,
      AuthMethod: PrincipalClaimValues.ServiceAccountCredentialMethod);

    var resourceA = new ResourceDescriptor(PermissionScopeKind.Device, deviceA.Id, tenantA.Id);
    var resultA = await evaluator.Evaluate(principal, PermissionNames.DeviceRead, resourceA, TestContext.Current.CancellationToken);
    Assert.True(resultA.Allowed);
    Assert.Equal("Direct", resultA.MatchedRuleSource);

    var resourceB = new ResourceDescriptor(PermissionScopeKind.Device, deviceB.Id, tenantB.Id);
    var resultB = await evaluator.Evaluate(principal, PermissionNames.DeviceRead, resourceB, TestContext.Current.CancellationToken);
    Assert.True(resultB.Allowed);
  }

  [Fact]
  public async Task ServerScopedDeviceAllow_OnTenantBoundUser_IsInert()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    var user = await testApp.App.Services.CreateTestUser(tenant.Id);
    var device = await testApp.App.Services.CreateTestDevice(tenant.Id);

    // Written directly, because the write boundary now rejects this row. A database that predates
    // that rule can still hold one, and it must not confer reach.
    await SeedAssignment(testApp, new PermissionAssignment
    {
      PrincipalKind = PermissionPrincipalKind.User,
      PrincipalId = user.Id,
      PermissionName = PermissionNames.DeviceRead,
      Effect = PermissionEffect.Allow,
      ScopeKind = PermissionScopeKind.Server,
      ScopeId = null,
      OwningTenantId = null,
      IsEnabled = true
    });

    var evaluator = GetEvaluator(testApp);
    var principal = CreateUserPrincipal(user.Id, tenant.Id);
    var resource = new ResourceDescriptor(PermissionScopeKind.Device, device.Id, tenant.Id);

    var result = await evaluator.Evaluate(principal, PermissionNames.DeviceRead, resource, TestContext.Current.CancellationToken);

    Assert.False(result.Allowed);
  }

  [Fact]
  public async Task ServerScopeDeny_OverridesTenantScopeAllow()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    var user = await testApp.App.Services.CreateTestUser(tenant.Id);
    var device = await testApp.App.Services.CreateTestDevice(tenant.Id);

    await SeedAssignment(testApp, new PermissionAssignment
    {
      PrincipalKind = PermissionPrincipalKind.User,
      PrincipalId = user.Id,
      PermissionName = PermissionNames.DeviceRead,
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
      PermissionName = PermissionNames.DeviceRead,
      Effect = PermissionEffect.Deny,
      ScopeKind = PermissionScopeKind.Server,
      ScopeId = null,
      IsEnabled = true
    });

    var evaluator = GetEvaluator(testApp);
    var principal = CreateUserPrincipal(user.Id, tenant.Id);
    var resource = new ResourceDescriptor(PermissionScopeKind.Device, device.Id, tenant.Id);

    var result = await evaluator.Evaluate(principal, PermissionNames.DeviceRead, resource, TestContext.Current.CancellationToken);

    Assert.False(result.Allowed);
  }

  [Fact]
  public async Task ServerServiceAccount_RestrictedAfterDeletingFinalAssignmentRow_DeniesInsteadOfEscalating()
  {
    // Deleting the final row of a restricted account must not escalate it to bypass.
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    var device = await testApp.App.Services.CreateTestDevice(tenant.Id);
    var serviceAccountId = await SeedServerServiceAccount(testApp, ServiceAccountAccessMode.Restricted);

    await SeedAssignment(testApp, new PermissionAssignment
    {
      PrincipalKind = PermissionPrincipalKind.ServiceAccount,
      PrincipalId = serviceAccountId,
      PermissionName = PermissionNames.DeviceRead,
      Effect = PermissionEffect.Allow,
      ScopeKind = PermissionScopeKind.Device,
      ScopeId = device.Id,
      IsEnabled = true
    });

    var evaluator = GetEvaluator(testApp);
    var principal = new PrincipalDescriptor(
      PrincipalType.ServerServiceAccount,
      serviceAccountId,
      TenantId: null,
      AuthMethod: PrincipalClaimValues.ServiceAccountCredentialMethod);
    var resource = new ResourceDescriptor(PermissionScopeKind.Device, device.Id, tenant.Id);

    var before = await evaluator.Evaluate(principal, PermissionNames.DeviceRead, resource, TestContext.Current.CancellationToken);
    Assert.True(before.Allowed);

    using (var scope = testApp.App.Services.CreateScope())
    {
      await using var db = scope.ServiceProvider.GetRequiredService<AppDb>();
      var rows = await db.PermissionAssignments
        .IgnoreQueryFilters()
        .Where(x => x.PrincipalKind == PermissionPrincipalKind.ServiceAccount && x.PrincipalId == serviceAccountId)
        .ToListAsync(TestContext.Current.CancellationToken);
      db.PermissionAssignments.RemoveRange(rows);
      await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    var after = await evaluator.Evaluate(principal, PermissionNames.DeviceRead, resource, TestContext.Current.CancellationToken);
    Assert.False(after.Allowed);
  }

  [Fact]
  public async Task ServerServiceAccount_RestrictedWithAllAssignmentsDisabled_DeniesInsteadOfBypass()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    var device = await testApp.App.Services.CreateTestDevice(tenant.Id);
    var serviceAccountId = await SeedServerServiceAccount(testApp, ServiceAccountAccessMode.Restricted);

    await SeedAssignment(testApp, new PermissionAssignment
    {
      PrincipalKind = PermissionPrincipalKind.ServiceAccount,
      PrincipalId = serviceAccountId,
      PermissionName = PermissionNames.DeviceRead,
      Effect = PermissionEffect.Allow,
      ScopeKind = PermissionScopeKind.Device,
      ScopeId = device.Id,
      IsEnabled = false
    });

    var evaluator = GetEvaluator(testApp);
    var principal = new PrincipalDescriptor(
      PrincipalType.ServerServiceAccount,
      serviceAccountId,
      TenantId: null,
      AuthMethod: PrincipalClaimValues.ServiceAccountCredentialMethod);
    var resource = new ResourceDescriptor(PermissionScopeKind.Device, device.Id, tenant.Id);

    var result = await evaluator.Evaluate(principal, PermissionNames.DeviceRead, resource, TestContext.Current.CancellationToken);

    Assert.False(result.Allowed);
  }

  [Fact]
  public async Task ServerServiceAccount_RestrictedWithAssignments_EvaluatesNormally()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    var device = await testApp.App.Services.CreateTestDevice(tenant.Id);
    var serviceAccountId = await SeedServerServiceAccount(testApp, ServiceAccountAccessMode.Restricted);

    await SeedAssignment(testApp, new PermissionAssignment
    {
      PrincipalKind = PermissionPrincipalKind.ServiceAccount,
      PrincipalId = serviceAccountId,
      PermissionName = PermissionNames.DeviceRead,
      Effect = PermissionEffect.Allow,
      ScopeKind = PermissionScopeKind.Device,
      ScopeId = device.Id,
      IsEnabled = true
    });

    var evaluator = GetEvaluator(testApp);
    var principal = new PrincipalDescriptor(
      PrincipalType.ServerServiceAccount,
      serviceAccountId,
      TenantId: null,
      AuthMethod: PrincipalClaimValues.ServiceAccountCredentialMethod);

    var allowedResource = new ResourceDescriptor(PermissionScopeKind.Device, device.Id, tenant.Id);
    var allowedResult = await evaluator.Evaluate(principal, PermissionNames.DeviceRead, allowedResource, TestContext.Current.CancellationToken);
    Assert.True(allowedResult.Allowed);

    var otherDevice = await testApp.App.Services.CreateTestDevice(tenant.Id);
    var deniedResource = new ResourceDescriptor(PermissionScopeKind.Device, otherDevice.Id, tenant.Id);
    var deniedResult = await evaluator.Evaluate(principal, PermissionNames.DeviceRead, deniedResource, TestContext.Current.CancellationToken);
    Assert.False(deniedResult.Allowed);
  }

  [Fact]
  public async Task ServerServiceAccount_RestrictedWithNoAssignments_Denies()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var serviceAccountId = await SeedServerServiceAccount(testApp, ServiceAccountAccessMode.Restricted);

    var evaluator = GetEvaluator(testApp);
    var principal = new PrincipalDescriptor(
      PrincipalType.ServerServiceAccount,
      serviceAccountId,
      TenantId: null,
      AuthMethod: PrincipalClaimValues.ServiceAccountCredentialMethod);
    var resource = new ResourceDescriptor(PermissionScopeKind.Device, Guid.NewGuid());

    var result = await evaluator.Evaluate(principal, PermissionNames.DeviceDelete, resource, TestContext.Current.CancellationToken);

    Assert.False(result.Allowed);
  }

  [Fact]
  public async Task ServerServiceAccount_UnrestrictedWithNoAssignments_Bypasses()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var serviceAccountId = await SeedServerServiceAccount(testApp, ServiceAccountAccessMode.Unrestricted);

    var evaluator = GetEvaluator(testApp);
    var principal = new PrincipalDescriptor(
      PrincipalType.ServerServiceAccount,
      serviceAccountId,
      TenantId: null,
      AuthMethod: PrincipalClaimValues.ServiceAccountCredentialMethod);
    var resource = new ResourceDescriptor(PermissionScopeKind.Device, Guid.NewGuid());

    var result = await evaluator.Evaluate(principal, PermissionNames.DeviceDelete, resource, TestContext.Current.CancellationToken);

    Assert.True(result.Allowed);
    Assert.Equal("server-service-account-bypass", result.MatchedRuleSource);
  }

  [Fact]
  public async Task TenantIsolation_WhenAssignmentInDifferentTenant_Denies()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenantA = await testApp.App.Services.CreateTestTenant("Tenant A");
    var tenantB = await testApp.App.Services.CreateTestTenant("Tenant B");
    var userA = await testApp.App.Services.CreateTestUser(tenantA.Id);
    var deviceB = await testApp.App.Services.CreateTestDevice(tenantB.Id);

    await SeedAssignment(testApp, new PermissionAssignment
    {
      PrincipalKind = PermissionPrincipalKind.User,
      PrincipalId = userA.Id,
      PermissionName = PermissionNames.DeviceRead,
      Effect = PermissionEffect.Allow,
      ScopeKind = PermissionScopeKind.Tenant,
      ScopeId = tenantA.Id,
      OwningTenantId = tenantA.Id,
      IsEnabled = true
    });

    var evaluator = GetEvaluator(testApp);
    var principal = CreateUserPrincipal(userA.Id, tenantA.Id);
    var resource = new ResourceDescriptor(PermissionScopeKind.Device, deviceB.Id, tenantB.Id);

    var result = await evaluator.Evaluate(principal, PermissionNames.DeviceRead, resource, TestContext.Current.CancellationToken);

    Assert.False(result.Allowed);
  }

  [Fact]
  public async Task TenantIsolation_WhenAssignmentOwnedByOtherTenant_Denies()
  {
    // The assignment is owned by tenant A and covers a tenant-A resource, but the
    // principal belongs to tenant B. Only the resolver's owning-tenant filter prevents
    // the row from becoming a rule; scope matching alone would allow it. This isolates
    // that boundary from the scope-match path.
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenantA = await testApp.App.Services.CreateTestTenant("Tenant A");
    var tenantB = await testApp.App.Services.CreateTestTenant("Tenant B");
    var userB = await testApp.App.Services.CreateTestUser(tenantB.Id);
    var deviceA = await testApp.App.Services.CreateTestDevice(tenantA.Id);

    await SeedAssignment(testApp, new PermissionAssignment
    {
      PrincipalKind = PermissionPrincipalKind.User,
      PrincipalId = userB.Id,
      PermissionName = PermissionNames.DeviceRead,
      Effect = PermissionEffect.Allow,
      ScopeKind = PermissionScopeKind.Tenant,
      ScopeId = tenantA.Id,
      OwningTenantId = tenantA.Id,
      IsEnabled = true
    });

    var evaluator = GetEvaluator(testApp);
    var principal = CreateUserPrincipal(userB.Id, tenantB.Id);
    var resource = new ResourceDescriptor(PermissionScopeKind.Device, deviceA.Id, tenantA.Id);

    var result = await evaluator.Evaluate(principal, PermissionNames.DeviceRead, resource, TestContext.Current.CancellationToken);

    Assert.False(result.Allowed);
    Assert.Contains("default deny", result.DenialReason);
  }

  [Fact]
  public async Task TenantScopeAssignment_CoversDeviceInTenant()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    var user = await testApp.App.Services.CreateTestUser(tenant.Id);
    var device = await testApp.App.Services.CreateTestDevice(tenant.Id);

    await SeedAssignment(testApp, new PermissionAssignment
    {
      PrincipalKind = PermissionPrincipalKind.User,
      PrincipalId = user.Id,
      PermissionName = PermissionNames.DeviceRead,
      Effect = PermissionEffect.Allow,
      ScopeKind = PermissionScopeKind.Tenant,
      ScopeId = tenant.Id,
      OwningTenantId = tenant.Id,
      IsEnabled = true
    });

    var evaluator = GetEvaluator(testApp);
    var principal = CreateUserPrincipal(user.Id, tenant.Id);
    var resource = new ResourceDescriptor(PermissionScopeKind.Device, device.Id, tenant.Id);

    var result = await evaluator.Evaluate(principal, PermissionNames.DeviceRead, resource, TestContext.Current.CancellationToken);

    Assert.True(result.Allowed);
    Assert.Equal("Direct", result.MatchedRuleSource);
  }

  [Fact]
  public async Task TenantServiceAccount_UnrestrictedAccessMode_IgnoredAndNotBypassed()
  {
    // A persisted Unrestricted mode must not grant bypass to a tenant-scoped account.
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    var accountId = Guid.NewGuid();

    using (var scope = testApp.App.Services.CreateScope())
    {
      await using var db = scope.ServiceProvider.GetRequiredService<AppDb>();
      db.ServiceAccounts.Add(new ServiceAccount
      {
        Id = accountId,
        Kind = ServiceAccountKind.Tenant,
        TenantId = tenant.Id,
        Name = $"tenant-sa-{accountId:N}",
        IsEnabled = true,
        AccessMode = ServiceAccountAccessMode.Unrestricted
      });
      await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    var evaluator = GetEvaluator(testApp);
    var principal = new PrincipalDescriptor(
      PrincipalType.TenantServiceAccount,
      accountId,
      TenantId: tenant.Id,
      AuthMethod: PrincipalClaimValues.ServiceAccountCredentialMethod);
    var resource = new ResourceDescriptor(PermissionScopeKind.Device, Guid.NewGuid());

    var result = await evaluator.Evaluate(principal, PermissionNames.DeviceDelete, resource, TestContext.Current.CancellationToken);

    Assert.False(result.Allowed);
  }

  [Fact]
  public async Task TenantServiceAccount_WithAssignment_Allows()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    var device = await testApp.App.Services.CreateTestDevice(tenant.Id);
    var serviceAccountId = Guid.NewGuid();

    await SeedAssignment(testApp, new PermissionAssignment
    {
      PrincipalKind = PermissionPrincipalKind.ServiceAccount,
      PrincipalId = serviceAccountId,
      PermissionName = PermissionNames.DeviceRead,
      Effect = PermissionEffect.Allow,
      ScopeKind = PermissionScopeKind.Tenant,
      ScopeId = tenant.Id,
      OwningTenantId = tenant.Id,
      IsEnabled = true
    });

    var evaluator = GetEvaluator(testApp);
    var principal = new PrincipalDescriptor(
      PrincipalType.TenantServiceAccount,
      serviceAccountId,
      TenantId: tenant.Id,
      AuthMethod: PrincipalClaimValues.ServiceAccountCredentialMethod);
    var resource = new ResourceDescriptor(PermissionScopeKind.Device, device.Id, tenant.Id);

    var result = await evaluator.Evaluate(principal, PermissionNames.DeviceRead, resource, TestContext.Current.CancellationToken);

    Assert.True(result.Allowed);
    Assert.Equal("Direct", result.MatchedRuleSource);
  }

  [Fact]
  public async Task UserGroupAllow_WhenGroupHasAssignment_Allows()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    var user = await testApp.App.Services.CreateTestUser(tenant.Id);
    var device = await testApp.App.Services.CreateTestDevice(tenant.Id);

    var groupId = Guid.NewGuid();
    await SeedUserGroup(testApp, groupId, tenant.Id, user.Id);

    await SeedAssignment(testApp, new PermissionAssignment
    {
      PrincipalKind = PermissionPrincipalKind.UserGroup,
      PrincipalId = groupId,
      PermissionName = PermissionNames.DeviceRead,
      Effect = PermissionEffect.Allow,
      ScopeKind = PermissionScopeKind.Device,
      ScopeId = device.Id,
      OwningTenantId = tenant.Id,
      IsEnabled = true
    });

    var evaluator = GetEvaluator(testApp);
    var principal = CreateUserPrincipal(user.Id, tenant.Id);
    var resource = new ResourceDescriptor(PermissionScopeKind.Device, device.Id, tenant.Id);

    var result = await evaluator.Evaluate(principal, PermissionNames.DeviceRead, resource, TestContext.Current.CancellationToken);

    Assert.True(result.Allowed);
    Assert.Equal("UserGroup", result.MatchedRuleSource);
  }

  private static PersonalAccessToken CreatePersonalAccessToken(
    Guid tokenId,
    Guid userId,
    PersonalAccessTokenPermissionMode permissionMode)
  {
    return new PersonalAccessToken
    {
      Id = tokenId,
      Name = $"pat-{tokenId:N}",
      HashedKey = "test-hashed-key",
      UserId = userId,
      PermissionMode = permissionMode
    };
  }

  private static PrincipalDescriptor CreateUserPrincipal(
    Guid userId,
    Guid tenantId,
    Guid? credentialId = null,
    CredentialType? credentialType = null,
    Guid? deviceScopeId = null)
  {
    return new PrincipalDescriptor(
      PrincipalType.User,
      userId,
      tenantId,
      AuthMethod: credentialType switch
      {
        CredentialType.PersonalAccessToken => PrincipalClaimValues.PersonalAccessTokenMethod,
        CredentialType.LogonToken => PrincipalClaimValues.LogonTokenMethod,
        _ => "cookie"
      },
      CredentialId: credentialId,
      CredentialType: credentialType,
      DeviceScopeId: deviceScopeId);
  }

  private static IPermissionEvaluator GetEvaluator(TestApp testApp)
  {
    return testApp.App.Services.GetRequiredService<IPermissionEvaluator>();
  }

  private static async Task SeedAssignment(TestApp testApp, PermissionAssignment assignment)
  {
    using var scope = testApp.App.Services.CreateScope();
    await using var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    db.PermissionAssignments.Add(assignment);
    await db.SaveChangesAsync();
  }

  /// <summary>
  /// Seeds a tenant, a user with a tenant-wide DeviceRead grant, a device, and a PAT
  /// row for that user with the given permission mode. The PAT has no scope rows.
  /// </summary>
  private static async Task<(AppUser User, Tenant Tenant, Device Device, Guid TokenId)> SeedPatScenario(
    TestApp testApp,
    PersonalAccessTokenPermissionMode permissionMode)
  {
    var tenant = await testApp.App.Services.CreateTestTenant();
    var user = await testApp.App.Services.CreateTestUser(tenant.Id);
    var device = await testApp.App.Services.CreateTestDevice(tenant.Id);
    var tokenId = Guid.NewGuid();

    await SeedAssignment(testApp, new PermissionAssignment
    {
      PrincipalKind = PermissionPrincipalKind.User,
      PrincipalId = user.Id,
      PermissionName = PermissionNames.DeviceRead,
      Effect = PermissionEffect.Allow,
      ScopeKind = PermissionScopeKind.Tenant,
      ScopeId = tenant.Id,
      OwningTenantId = tenant.Id,
      IsEnabled = true
    });

    using (var scope = testApp.App.Services.CreateScope())
    {
      await using var db = scope.ServiceProvider.GetRequiredService<AppDb>();
      db.PersonalAccessTokens.Add(CreatePersonalAccessToken(tokenId, user.Id, permissionMode));
      await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    return (user, tenant, device, tokenId);
  }

  private static async Task SeedPersonalAccessToken(
    TestApp testApp,
    Guid userId,
    Guid tokenId,
    PersonalAccessTokenPermissionMode permissionMode)
  {
    using var scope = testApp.App.Services.CreateScope();
    await using var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    db.PersonalAccessTokens.Add(CreatePersonalAccessToken(tokenId, userId, permissionMode));
    await db.SaveChangesAsync();
  }

  private static async Task<Guid> SeedServerServiceAccount(TestApp testApp, ServiceAccountAccessMode accessMode)
  {
    var accountId = Guid.NewGuid();
    using var scope = testApp.App.Services.CreateScope();
    await using var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    db.ServiceAccounts.Add(new ServiceAccount
    {
      Id = accountId,
      Kind = ServiceAccountKind.Server,
      Name = $"server-sa-{accountId:N}",
      IsEnabled = true,
      AccessMode = accessMode
    });
    await db.SaveChangesAsync();
    return accountId;
  }

  private static async Task SeedUserGroup(TestApp testApp, Guid groupId, Guid tenantId, Guid userId)
  {
    using var scope = testApp.App.Services.CreateScope();
    await using var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    db.UserGroups.Add(new UserGroup { Id = groupId, Name = $"group-{groupId:N}", TenantId = tenantId });
    db.UserGroupMembers.Add(new UserGroupMember { UserGroupId = groupId, UserId = userId });
    await db.SaveChangesAsync();
  }
}
