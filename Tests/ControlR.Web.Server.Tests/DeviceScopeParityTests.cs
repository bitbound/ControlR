using System.Security.Claims;
using ControlR.Web.Server.Authn;
using ControlR.Web.Server.Authz.Permissions;
using ControlR.Web.Server.Data;
using ControlR.Web.Server.Data.Entities;
using ControlR.Web.Server.Extensions.Database;
using ControlR.Web.Server.Services.Authorization;
using ControlR.Web.Server.Services.DeviceManagement;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ControlR.Web.Server.Tests;

/// <summary>
/// Parity tests asserting that the set-enumeration path (<see cref="IDeviceAccessScopeResolver"/>
/// projected through <c>ApplyAccessScope</c>) and the point-authorization path
/// (<see cref="IPermissionEvaluator"/>) agree on which devices a principal may access. Both paths
/// interpret assignments through the shared permission evaluation context; these tests use
/// assignment-scoped principals (no role bridges) across each scope kind.
/// </summary>
public class DeviceScopeParityTests(ITestOutputHelper testOutput)
{
  private readonly ITestOutputHelper _testOutput = testOutput;

  [Fact]
  public async Task Parity_CustomerScopedAssignment()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    var user = await testApp.App.Services.CreateTestUser(tenant.Id);
    var deviceA = await testApp.App.Services.CreateTestDevice(tenant.Id);
    var deviceB = await testApp.App.Services.CreateTestDevice(tenant.Id);
    var customerId = Guid.NewGuid();

    using (var scope = testApp.App.Services.CreateScope())
    {
      await using var db = scope.ServiceProvider.GetRequiredService<AppDb>();
      db.Customers.Add(new Customer { Id = customerId, Name = $"customer-{customerId:N}", TenantId = tenant.Id });
      await db.SaveChangesAsync(TestContext.Current.CancellationToken);

      var device = await db.Devices.FindAsync([deviceA.Id], TestContext.Current.CancellationToken);
      device!.CustomerId = customerId;
      await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

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

    var (claims, principal) = CreateUserPrincipalPair(user.Id, tenant.Id);

    await AssertResolverEvaluatorParity(testApp, tenant.Id, claims, principal,
    [
      new ParityDevice(deviceA.Id, customerId, []),
      new ParityDevice(deviceB.Id, null, [])
    ]);
  }

  [Fact]
  public async Task Parity_DeviceGroupScopedAssignment()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    var user = await testApp.App.Services.CreateTestUser(tenant.Id);
    var deviceA = await testApp.App.Services.CreateTestDevice(tenant.Id);
    var deviceB = await testApp.App.Services.CreateTestDevice(tenant.Id);
    var groupId = Guid.NewGuid();

    using (var scope = testApp.App.Services.CreateScope())
    {
      await using var db = scope.ServiceProvider.GetRequiredService<AppDb>();
      db.DeviceGroups.Add(new DeviceGroup { Id = groupId, Name = $"group-{groupId:N}", TenantId = tenant.Id });
      db.DeviceGroupMembers.Add(new DeviceGroupMember { DeviceId = deviceA.Id, DeviceGroupId = groupId });
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

    var (claims, principal) = CreateUserPrincipalPair(user.Id, tenant.Id);

    await AssertResolverEvaluatorParity(testApp, tenant.Id, claims, principal,
    [
      new ParityDevice(deviceA.Id, null, [groupId]),
      new ParityDevice(deviceB.Id, null, [])
    ]);
  }

  [Fact]
  public async Task Parity_DeviceScopedAssignment()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    var user = await testApp.App.Services.CreateTestUser(tenant.Id);
    var deviceA = await testApp.App.Services.CreateTestDevice(tenant.Id);
    var deviceB = await testApp.App.Services.CreateTestDevice(tenant.Id);

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

    var (claims, principal) = CreateUserPrincipalPair(user.Id, tenant.Id);

    await AssertResolverEvaluatorParity(testApp, tenant.Id, claims, principal,
    [
      new ParityDevice(deviceA.Id, null, []),
      new ParityDevice(deviceB.Id, null, [])
    ]);
  }

  [Fact]
  public async Task Parity_DisabledAllowAssignment_DoesNotGrant()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    var user = await testApp.App.Services.CreateTestUser(tenant.Id);
    var deviceA = await testApp.App.Services.CreateTestDevice(tenant.Id);

    await SeedAssignment(testApp, new PermissionAssignment
    {
      PrincipalKind = PermissionPrincipalKind.User,
      PrincipalId = user.Id,
      PermissionName = PermissionNames.DeviceRead,
      Effect = PermissionEffect.Allow,
      ScopeKind = PermissionScopeKind.Tenant,
      ScopeId = tenant.Id,
      OwningTenantId = tenant.Id,
      IsEnabled = false
    });

    var (claims, principal) = CreateUserPrincipalPair(user.Id, tenant.Id);

    // Assert the absolute expected outcome (deny + empty enumeration), not just parity, so the
    // test fails if both the resolver and the evaluator incorrectly grant access.
    await AssertResolverAndEvaluatorDenyAll(testApp, tenant.Id, claims, principal,
    [
      new ParityDevice(deviceA.Id, null, [])
    ]);
  }

  [Fact]
  public async Task Parity_MultiCategoryAllows_EnumeratesAllCategories()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    var user = await testApp.App.Services.CreateTestUser(tenant.Id);
    var deviceA = await testApp.App.Services.CreateTestDevice(tenant.Id);
    var deviceB = await testApp.App.Services.CreateTestDevice(tenant.Id);
    var deviceC = await testApp.App.Services.CreateTestDevice(tenant.Id);
    var groupId = Guid.NewGuid();
    var customerId = Guid.NewGuid();

    using (var scope = testApp.App.Services.CreateScope())
    {
      await using var db = scope.ServiceProvider.GetRequiredService<AppDb>();
      db.DeviceGroups.Add(new DeviceGroup { Id = groupId, Name = $"group-{groupId:N}", TenantId = tenant.Id });
      db.DeviceGroupMembers.Add(new DeviceGroupMember { DeviceId = deviceA.Id, DeviceGroupId = groupId });
      db.Customers.Add(new Customer { Id = customerId, Name = $"customer-{customerId:N}", TenantId = tenant.Id });
      await db.SaveChangesAsync(TestContext.Current.CancellationToken);

      var device = await db.Devices.FindAsync([deviceB.Id], TestContext.Current.CancellationToken);
      device!.CustomerId = customerId;
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
      PrincipalKind = PermissionPrincipalKind.User,
      PrincipalId = user.Id,
      PermissionName = PermissionNames.DeviceRead,
      Effect = PermissionEffect.Allow,
      ScopeKind = PermissionScopeKind.CustomerTenant,
      ScopeId = customerId,
      OwningTenantId = tenant.Id,
      IsEnabled = true
    });

    var (claims, principal) = CreateUserPrincipalPair(user.Id, tenant.Id);

    await AssertResolverEvaluatorParity(testApp, tenant.Id, claims, principal,
    [
      new ParityDevice(deviceA.Id, null, [groupId]),
      new ParityDevice(deviceB.Id, customerId, []),
      new ParityDevice(deviceC.Id, null, [])
    ]);
  }

  [Fact]
  public async Task Parity_PatWithExplicitDeviceScope_EnumeratesOnlyBoundedDevice()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    var user = await testApp.App.Services.CreateTestUser(tenant.Id);
    var deviceA = await testApp.App.Services.CreateTestDevice(tenant.Id);
    var deviceB = await testApp.App.Services.CreateTestDevice(tenant.Id);
    var patId = Guid.NewGuid();

    await SeedAssignment(testApp, PermissionAssignment.CreateGrant(
      PermissionPrincipalKind.User,
      user.Id,
      PermissionNames.DeviceRead,
      PermissionScopeKind.Tenant,
      tenant.Id,
      tenant.Id,
      new PrincipalDescriptor(PrincipalType.User, user.Id, tenant.Id, "test")));
    await SeedAssignment(testApp, PermissionAssignment.CreateGrant(
      PermissionPrincipalKind.PersonalAccessToken,
      patId,
      PermissionNames.DeviceRead,
      PermissionScopeKind.Device,
      deviceA.Id,
      tenant.Id,
      new PrincipalDescriptor(PrincipalType.User, user.Id, tenant.Id, "test")));

    var (claims, principal) = CreateCredentialPrincipalPair(
      user.Id,
      tenant.Id,
      patId,
      CredentialType.PersonalAccessToken);

    await AssertResolverEvaluatorParity(testApp, tenant.Id, claims, principal,
    [
      new ParityDevice(deviceA.Id, null, []),
      new ParityDevice(deviceB.Id, null, [])
    ]);
  }

  [Fact]
  public async Task Parity_RestrictedPatWithoutRows_EnumeratesNothing()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    var user = await testApp.App.Services.CreateTestUser(tenant.Id);
    var deviceA = await testApp.App.Services.CreateTestDevice(tenant.Id);
    var patId = Guid.NewGuid();

    // The owner holds a tenant-wide grant, but the Restricted PAT has no rows of its own,
    // so neither enumeration nor point evaluation may grant access.
    await SeedAssignment(testApp, PermissionAssignment.CreateGrant(
      PermissionPrincipalKind.User,
      user.Id,
      PermissionNames.DeviceRead,
      PermissionScopeKind.Tenant,
      tenant.Id,
      tenant.Id,
      new PrincipalDescriptor(PrincipalType.User, user.Id, tenant.Id, "test")));

    var (claims, principal) = CreateCredentialPrincipalPair(
      user.Id, tenant.Id, patId, CredentialType.PersonalAccessToken);

    // Assert the absolute expected outcome (deny + empty enumeration), not just parity, so the
    // test fails if both the resolver and the evaluator incorrectly grant access.
    await AssertResolverAndEvaluatorDenyAll(testApp, tenant.Id, claims, principal,
    [
      new ParityDevice(deviceA.Id, null, [])
    ]);
  }

  [Fact]
  public async Task Parity_ServerServiceAccountWithDeviceScope_EnumeratesAcrossTenantsOnlyMatchingDevice()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenantA = await testApp.App.Services.CreateTestTenant("Tenant A");
    var tenantB = await testApp.App.Services.CreateTestTenant("Tenant B");
    var deviceA = await testApp.App.Services.CreateTestDevice(tenantA.Id);
    var deviceB = await testApp.App.Services.CreateTestDevice(tenantB.Id);
    var serviceAccountId = Guid.NewGuid();
    await SeedAssignment(testApp, PermissionAssignment.CreateGrant(
      PermissionPrincipalKind.ServiceAccount,
      serviceAccountId,
      PermissionNames.DeviceRead,
      PermissionScopeKind.Device,
      deviceB.Id,
      tenantB.Id,
      new PrincipalDescriptor(PrincipalType.ServerServiceAccount, serviceAccountId, null, "test")));

    var claims = new ClaimsPrincipal(new ClaimsIdentity(
    [
      new Claim(PrincipalClaimTypes.PrincipalType, PrincipalClaimValues.ServerServiceAccount),
      new Claim(PrincipalClaimTypes.PrincipalId, serviceAccountId.ToString())
    ], "TestAuth"));
    var principal = claims.ToPrincipalDescriptor()
      ?? throw new InvalidOperationException("Failed to build principal descriptor from claims.");

    using var scope = testApp.App.Services.CreateScope();
    await using var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    var resolver = scope.ServiceProvider.GetRequiredService<IDeviceAccessScopeResolver>();
    var evaluator = scope.ServiceProvider.GetRequiredService<IPermissionEvaluator>();
    var accessScope = await resolver.Resolve(claims, TestContext.Current.CancellationToken);
    var listedIds = await db.Devices
      .IgnoreQueryFilters()
      .ApplyAccessScope(accessScope)
      .Select(device => device.Id)
      .ToListAsync(TestContext.Current.CancellationToken);

    Assert.DoesNotContain(deviceA.Id, listedIds);
    Assert.Contains(deviceB.Id, listedIds);
    Assert.False((await evaluator.Evaluate(
      principal,
      PermissionNames.DeviceRead,
      new ResourceDescriptor(PermissionScopeKind.Device, deviceA.Id, tenantA.Id),
      TestContext.Current.CancellationToken)).Allowed);
    Assert.True((await evaluator.Evaluate(
      principal,
      PermissionNames.DeviceRead,
      new ResourceDescriptor(PermissionScopeKind.Device, deviceB.Id, tenantB.Id),
      TestContext.Current.CancellationToken)).Allowed);
  }

  [Fact]
  public async Task Parity_TenantAllowWithDeviceDeny_ExcludesDeniedDevice()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    var user = await testApp.App.Services.CreateTestUser(tenant.Id);
    var deviceA = await testApp.App.Services.CreateTestDevice(tenant.Id);
    var deviceB = await testApp.App.Services.CreateTestDevice(tenant.Id);

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

    var (claims, principal) = CreateUserPrincipalPair(user.Id, tenant.Id);

    await AssertResolverEvaluatorParity(testApp, tenant.Id, claims, principal,
    [
      new ParityDevice(deviceA.Id, null, []),
      new ParityDevice(deviceB.Id, null, [])
    ]);
  }

  [Fact]
  public async Task Parity_TenantScopedAssignment()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    var user = await testApp.App.Services.CreateTestUser(tenant.Id);
    var deviceA = await testApp.App.Services.CreateTestDevice(tenant.Id);
    var deviceB = await testApp.App.Services.CreateTestDevice(tenant.Id);

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

    var (claims, principal) = CreateUserPrincipalPair(user.Id, tenant.Id);

    await AssertResolverEvaluatorParity(testApp, tenant.Id, claims, principal,
    [
      new ParityDevice(deviceA.Id, null, []),
      new ParityDevice(deviceB.Id, null, [])
    ]);
  }

  [Fact]
  public async Task Parity_UnrestrictedServerServiceAccount_EnumeratesAllDevices()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenantA = await testApp.App.Services.CreateTestTenant("Tenant A");
    var tenantB = await testApp.App.Services.CreateTestTenant("Tenant B");
    var deviceA = await testApp.App.Services.CreateTestDevice(tenantA.Id);
    var deviceB = await testApp.App.Services.CreateTestDevice(tenantB.Id);
    var serviceAccountId = Guid.NewGuid();

    using (var setupScope = testApp.App.Services.CreateScope())
    {
      await using var setupDb = setupScope.ServiceProvider.GetRequiredService<AppDb>();
      setupDb.ServiceAccounts.Add(new ServiceAccount
      {
        Id = serviceAccountId,
        Name = "unrestricted-sa",
        Kind = ServiceAccountKind.Server,
        AccessMode = ServiceAccountAccessMode.Unrestricted,
        IsEnabled = true
      });
      await setupDb.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    var claims = new ClaimsPrincipal(new ClaimsIdentity(
    [
      new Claim(PrincipalClaimTypes.PrincipalType, PrincipalClaimValues.ServerServiceAccount),
      new Claim(PrincipalClaimTypes.PrincipalId, serviceAccountId.ToString())
    ], "TestAuth"));
    var principal = claims.ToPrincipalDescriptor()
      ?? throw new InvalidOperationException("Failed to build principal descriptor from claims.");

    using var scope = testApp.App.Services.CreateScope();
    await using var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    var resolver = scope.ServiceProvider.GetRequiredService<IDeviceAccessScopeResolver>();
    var evaluator = scope.ServiceProvider.GetRequiredService<IPermissionEvaluator>();
    var accessScope = await resolver.Resolve(claims, TestContext.Current.CancellationToken);
    var listedIds = await db.Devices
      .IgnoreQueryFilters()
      .ApplyAccessScope(accessScope)
      .Select(device => device.Id)
      .ToListAsync(TestContext.Current.CancellationToken);

    Assert.Contains(deviceA.Id, listedIds);
    Assert.Contains(deviceB.Id, listedIds);
    Assert.True((await evaluator.Evaluate(
      principal,
      PermissionNames.DeviceRead,
      new ResourceDescriptor(PermissionScopeKind.Device, deviceA.Id, tenantA.Id),
      TestContext.Current.CancellationToken)).Allowed);
    Assert.True((await evaluator.Evaluate(
      principal,
      PermissionNames.DeviceRead,
      new ResourceDescriptor(PermissionScopeKind.Device, deviceB.Id, tenantB.Id),
      TestContext.Current.CancellationToken)).Allowed);
  }

  /// <summary>
  /// Asserts the absolute expected outcome for a deny scenario: the enumeration returns no
  /// devices and the evaluator denies every listed device. Unlike
  /// <see cref="AssertResolverEvaluatorParity"/>, this fails when both paths share the same bug
  /// and wrongly grant access.
  /// </summary>
  private static async Task AssertResolverAndEvaluatorDenyAll(
    TestApp testApp,
    Guid tenantId,
    ClaimsPrincipal claimsPrincipal,
    PrincipalDescriptor principal,
    IReadOnlyList<ParityDevice> devices)
  {
    var cancellationToken = TestContext.Current.CancellationToken;
    using var scope = testApp.App.Services.CreateScope();
    await using var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    var resolver = scope.ServiceProvider.GetRequiredService<IDeviceAccessScopeResolver>();
    var evaluator = scope.ServiceProvider.GetRequiredService<IPermissionEvaluator>();

    var accessScope = await resolver.Resolve(claimsPrincipal, cancellationToken);

    var listedDeviceIds = await db.Devices
      .ApplyAccessScope(tenantId, accessScope)
      .Select(x => x.Id)
      .ToListAsync(cancellationToken);

    Assert.Empty(listedDeviceIds);

    foreach (var device in devices)
    {
      var descriptor = new ResourceDescriptor(
        PermissionScopeKind.Device, device.Id, tenantId, device.CustomerId, DeviceGroupIds: device.GroupIds);

      var result = await evaluator.Evaluate(principal, PermissionNames.DeviceRead, descriptor, cancellationToken);

      Assert.False(result.Allowed);
    }
  }

  private static async Task AssertResolverEvaluatorParity(
    TestApp testApp,
    Guid tenantId,
    ClaimsPrincipal claimsPrincipal,
    PrincipalDescriptor principal,
    IReadOnlyList<ParityDevice> devices)
  {
    var cancellationToken = TestContext.Current.CancellationToken;
    using var scope = testApp.App.Services.CreateScope();
    await using var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    var resolver = scope.ServiceProvider.GetRequiredService<IDeviceAccessScopeResolver>();
    var evaluator = scope.ServiceProvider.GetRequiredService<IPermissionEvaluator>();

    var accessScope = await resolver.Resolve(claimsPrincipal, cancellationToken);

    var listedDeviceIds = await db.Devices
      .ApplyAccessScope(tenantId, accessScope)
      .Select(x => x.Id)
      .ToListAsync(cancellationToken);

    foreach (var device in devices)
    {
      var descriptor = new ResourceDescriptor(
        PermissionScopeKind.Device, device.Id, tenantId, device.CustomerId, DeviceGroupIds: device.GroupIds);

      var result = await evaluator.Evaluate(principal, PermissionNames.DeviceRead, descriptor, cancellationToken);

      Assert.Equal(result.Allowed, listedDeviceIds.Contains(device.Id));
    }
  }

  private static (ClaimsPrincipal Claims, PrincipalDescriptor Descriptor) CreateCredentialPrincipalPair(
    Guid userId,
    Guid tenantId,
    Guid credentialId,
    CredentialType credentialType)
  {
    var claims = new ClaimsPrincipal(new ClaimsIdentity(
    [
      new Claim(PrincipalClaimTypes.PrincipalType, PrincipalClaimValues.User),
      new Claim(PrincipalClaimTypes.PrincipalId, userId.ToString()),
      new Claim(UserClaimTypes.TenantId, tenantId.ToString()),
      new Claim(PrincipalClaimTypes.CredentialId, credentialId.ToString()),
      new Claim(PrincipalClaimTypes.CredentialType, credentialType.ToString())
    ], "TestAuth"));
    var descriptor = claims.ToPrincipalDescriptor()
      ?? throw new InvalidOperationException("Failed to build principal descriptor from claims.");
    return (claims, descriptor);
  }

  private static (ClaimsPrincipal Claims, PrincipalDescriptor Descriptor) CreateUserPrincipalPair(
    Guid userId, Guid tenantId)
  {
    var claims = new ClaimsPrincipal(new ClaimsIdentity(
    [
      new Claim(PrincipalClaimTypes.PrincipalType, PrincipalClaimValues.User),
      new Claim(PrincipalClaimTypes.PrincipalId, userId.ToString()),
      new Claim(UserClaimTypes.TenantId, tenantId.ToString())
    ], "TestAuth"));

    var descriptor = claims.ToPrincipalDescriptor()
      ?? throw new InvalidOperationException("Failed to build principal descriptor from claims.");

    return (claims, descriptor);
  }

  private static async Task SeedAssignment(TestApp testApp, PermissionAssignment assignment)
  {
    using var scope = testApp.App.Services.CreateScope();
    await using var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    db.PermissionAssignments.Add(assignment);
    await db.SaveChangesAsync(TestContext.Current.CancellationToken);
  }

  private sealed record ParityDevice(Guid Id, Guid? CustomerId, IReadOnlyCollection<Guid> GroupIds);
}
