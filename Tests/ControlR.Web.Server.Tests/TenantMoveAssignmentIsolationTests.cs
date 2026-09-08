using ControlR.Web.Server.Authz.Permissions;
using ControlR.Web.Server.Data;
using ControlR.Web.Server.Data.Entities;
using ControlR.Web.Server.Services;
using ControlR.Web.Server.Services.Authorization;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ControlR.Web.Server.Tests;

/// <summary>
/// Cross-tenant invite moves must not retain the old tenant's authorization: stale
/// assignment rows (any scope shape) and group memberships from the former tenant must be
/// inert after the move, both via the rule resolver's tenant-ownership boundary and via
/// cleanup at invite acceptance.
/// </summary>
public class TenantMoveAssignmentIsolationTests(ITestOutputHelper testOutput)
{
  private readonly ITestOutputHelper _testOutput = testOutput;

  [Fact]
  public async Task AcceptInvite_ClearsOldTenantAssignmentsAndMemberships()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var (tenantA, tenantB, user, deviceA, _) = await SetupMovedUserScenario(testApp);

    var activationCode = Guid.NewGuid().ToString("N");
    using (var scope = testApp.CreateScope())
    {
      await using var db = scope.ServiceProvider.GetRequiredService<AppDb>();
      db.TenantInvites.Add(new TenantInvite
      {
        TenantId = tenantB.Id,
        ActivationCode = activationCode,
        InviteeEmail = user.Email!.ToLower()
      });
      await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    using (var scope = testApp.CreateScope())
    {
      var invitesProvider = scope.ServiceProvider.GetRequiredService<ITenantInvitesProvider>();
      var acceptResult = await invitesProvider.AcceptInvite(
        new InternalDtos.AcceptInvitationRequestDto(activationCode, user.Email!, "N3wTenantPass!"));
      Assert.True(acceptResult.IsSuccess, $"AcceptInvite failed: {acceptResult.Reason}");
    }

    using (var scope = testApp.CreateScope())
    {
      await using var db = scope.ServiceProvider.GetRequiredService<AppDb>();

      var movedUser = await db.Users.IgnoreQueryFilters().FirstAsync(x => x.Id == user.Id, TestContext.Current.CancellationToken);
      Assert.Equal(tenantB.Id, movedUser.TenantId);

      // Former-tenant rows must be gone. The destination-tenant self-service baseline is
      // re-seeded on move (invited users keep managing their own PATs), so only rows owned
      // by the former tenant are asserted away.
      var remainingAssignments = await db.PermissionAssignments
        .IgnoreQueryFilters()
        .Where(x => x.PrincipalKind == PermissionPrincipalKind.User &&
                    x.PrincipalId == user.Id &&
                    x.OwningTenantId != tenantB.Id)
        .CountAsync(TestContext.Current.CancellationToken);
      Assert.Equal(0, remainingAssignments);

      var remainingMemberships = await db.UserGroupMembers
        .IgnoreQueryFilters()
        .Where(x => x.UserId == user.Id)
        .CountAsync(TestContext.Current.CancellationToken);
      Assert.Equal(0, remainingMemberships);
    }

    using (var scope = testApp.CreateScope())
    {
      var evaluator = scope.ServiceProvider.GetRequiredService<IPermissionEvaluator>();
      var resource = await BuildDeviceResource(scope, deviceA, tenantA.Id, (await GetGroupIds(scope, deviceA)).First());

      var result = await evaluator.Evaluate(
        UserPrincipal(user.Id, tenantB.Id), PermissionNames.DeviceRead, resource, TestContext.Current.CancellationToken);
      Assert.False(result.Allowed);
    }
  }

  [Fact]
  public async Task AcceptInvite_ClearsPatScopeRowsFromFormerTenant()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var (tenantA, tenantB, user, deviceA, _) = await SetupMovedUserScenario(testApp);

    // Create a PAT owned by the user, with a tenant-A scope row.
    var tokenId = Guid.NewGuid();
    using (var scope = testApp.CreateScope())
    {
      await using var db = scope.ServiceProvider.GetRequiredService<AppDb>();
      db.PersonalAccessTokens.Add(new PersonalAccessToken
      {
        Id = tokenId,
        Name = "mover-pat",
        HashedKey = "hash",
        UserId = user.Id
      });
      db.PermissionAssignments.Add(PermissionAssignment.CreateGrant(
        PermissionPrincipalKind.PersonalAccessToken,
        tokenId,
        PermissionNames.DeviceRead,
        PermissionScopeKind.Tenant,
        tenantA.Id,
        tenantA.Id,
        new PrincipalDescriptor(PrincipalType.User, user.Id, tenantA.Id, "test")));
      await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    var activationCode = Guid.NewGuid().ToString("N");
    using (var scope = testApp.CreateScope())
    {
      await using var db = scope.ServiceProvider.GetRequiredService<AppDb>();
      db.TenantInvites.Add(new TenantInvite
      {
        TenantId = tenantB.Id,
        ActivationCode = activationCode,
        InviteeEmail = user.Email!.ToLower()
      });
      await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    using (var scope = testApp.CreateScope())
    {
      var invitesProvider = scope.ServiceProvider.GetRequiredService<ITenantInvitesProvider>();
      var acceptResult = await invitesProvider.AcceptInvite(
        new InternalDtos.AcceptInvitationRequestDto(activationCode, user.Email!, "N3wTenantPass!"));
      Assert.True(acceptResult.IsSuccess, $"AcceptInvite failed: {acceptResult.Reason}");
    }

    using var verifyScope = testApp.CreateScope();
    await using var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDb>();
    var remainingPatScopeRows = await verifyDb.PermissionAssignments
      .IgnoreQueryFilters()
      .Where(x => x.PrincipalKind == PermissionPrincipalKind.PersonalAccessToken &&
                  x.PrincipalId == tokenId)
      .CountAsync(TestContext.Current.CancellationToken);
    Assert.Equal(0, remainingPatScopeRows);
  }

  [Fact]
  public async Task Evaluate_AfterTenantMove_GroupScopedStaleAssignment_Denies()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var (tenantA, tenantB, user, deviceA, groupId) = await SetupMovedUserScenario(testApp);

    using var scope = testApp.CreateScope();
    var evaluator = scope.ServiceProvider.GetRequiredService<IPermissionEvaluator>();
    var resource = await BuildDeviceResource(scope, deviceA, tenantA.Id, groupId);

    // Baseline: while the user belongs to tenant A, the group grant allows access.
    var beforeMove = await evaluator.Evaluate(
      UserPrincipal(user.Id, tenantA.Id), PermissionNames.DeviceRead, resource, TestContext.Current.CancellationToken);
    Assert.True(beforeMove.Allowed);

    // After the move, the stale group-scoped row owned by tenant A must be inert.
    var afterMove = await evaluator.Evaluate(
      UserPrincipal(user.Id, tenantB.Id), PermissionNames.DeviceRead, resource, TestContext.Current.CancellationToken);
    Assert.False(afterMove.Allowed);
  }

  private static async Task<ResourceDescriptor> BuildDeviceResource(
    IServiceScope scope, Guid deviceId, Guid tenantId, Guid groupId)
  {
    await using var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    var device = await db.Devices.IgnoreQueryFilters().AsNoTracking().FirstAsync(x => x.Id == deviceId);
    return new ResourceDescriptor(
      PermissionScopeKind.Device, deviceId, tenantId, device.CustomerId, DeviceGroupIds: [groupId]);
  }

  private static async Task<List<Guid>> GetGroupIds(IServiceScope scope, Guid deviceId)
  {
    await using var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    return await db.DeviceGroupMembers
      .IgnoreQueryFilters()
      .Where(x => x.DeviceId == deviceId)
      .Select(x => x.DeviceGroupId)
      .ToListAsync(TestContext.Current.CancellationToken);
  }

  private static PrincipalDescriptor UserPrincipal(Guid userId, Guid tenantId) =>
    new(
      PrincipalType: PrincipalType.User,
      PrincipalId: userId,
      TenantId: tenantId,
      AuthMethod: "tenant-move-isolation-test");

  private async Task<(Tenant TenantA, Tenant TenantB, AppUser User, Guid DeviceA, Guid GroupId)> SetupMovedUserScenario(
    TestApp testApp)
  {
    var tenantA = await testApp.App.Services.CreateTestTenant("Tenant A");
    var tenantB = await testApp.App.Services.CreateTestTenant("Tenant B");
    await testApp.App.Services.CreateTestUser(tenantA.Id, email: $"seed-{Guid.NewGuid():N}@t.local");
    var user = await testApp.App.Services.CreateTestUser(tenantA.Id, $"mover-{Guid.NewGuid():N}@t.local");
    var deviceA = await testApp.App.Services.CreateTestDevice(tenantA.Id);
    await testApp.App.Services.CreateTestDevice(tenantB.Id);
    var deviceGroupId = Guid.NewGuid();
    var userGroupId = Guid.NewGuid();

    using (var scope = testApp.CreateScope())
    {
      await using var db = scope.ServiceProvider.GetRequiredService<AppDb>();
      db.DeviceGroups.Add(new DeviceGroup { Id = deviceGroupId, Name = $"group-{deviceGroupId:N}", TenantId = tenantA.Id });
      db.DeviceGroupMembers.Add(new DeviceGroupMember { DeviceId = deviceA.Id, DeviceGroupId = deviceGroupId });
      db.UserGroups.Add(new UserGroup { Id = userGroupId, Name = $"ugroup-{userGroupId:N}", TenantId = tenantA.Id });
      db.UserGroupMembers.Add(new UserGroupMember { UserId = user.Id, UserGroupId = userGroupId });

      db.PermissionAssignments.Add(PermissionAssignment.CreateGrant(
        PermissionPrincipalKind.UserGroup,
        userGroupId,
        PermissionNames.DeviceRead,
        PermissionScopeKind.DeviceGroup,
        deviceGroupId,
        tenantA.Id,
        new PrincipalDescriptor(PrincipalType.User, user.Id, tenantA.Id, "test")));

      db.PermissionAssignments.Add(PermissionAssignment.CreateGrant(
        PermissionPrincipalKind.User,
        user.Id,
        PermissionNames.DeviceLogsRead,
        PermissionScopeKind.Tenant,
        tenantA.Id,
        tenantA.Id,
        new PrincipalDescriptor(PrincipalType.User, user.Id, tenantA.Id, "test")));

      await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    return (tenantA, tenantB, user, deviceA.Id, deviceGroupId);
  }
}
