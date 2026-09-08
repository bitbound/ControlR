using ControlR.Web.Server.Data;
using ControlR.Web.Server.Data.Entities;
using ControlR.Web.Server.Services.Authorization;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ControlR.Web.Server.Tests;

/// <summary>
/// Locks the trim sweeper's contract: it removes excess <b>reach</b> (Allow rows the owner
/// no longer effectively holds) and never removes denies, which confer no reach and are
/// deliberately allowed to be unowned by the owner (invariant I2).
/// </summary>
public class PatScopeTrimTests(ITestOutputHelper testOutput)
{
  private readonly ITestOutputHelper _testOutput = testOutput;

  [Fact]
  public async Task Sweep_WhenOwnerCannotCoverAllowRow_DeletesAllowRowAndLogsTrim()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput,
      testDatabaseName: $"{Guid.NewGuid()}");

    var tenant = await testApp.Services.CreateTestTenant();
    var userId = (await testApp.Services.CreateTestUser(tenant.Id, $"owner-{Guid.NewGuid():N}@t.local")).Id;
    var tokenId = Guid.NewGuid();
    await SeedPersonalAccessToken(testApp, tokenId, userId);

    var allowRow = PermissionAssignment.CreateGrant(
      PermissionPrincipalKind.PersonalAccessToken, tokenId,
      PermissionNames.DeviceRead, PermissionScopeKind.Tenant, null,
      tenant.Id, createdBy: null);
    await SeedAssignment(testApp, allowRow);

    var service = testApp.Services.GetRequiredService<PatScopeTrimBackgroundService>();
    await service.Sweep(TestContext.Current.CancellationToken);

    using var verifyScope = testApp.CreateScope();
    await using var db = verifyScope.ServiceProvider.GetRequiredService<AppDb>();
    var surviving = await db.PermissionAssignments
      .IgnoreQueryFilters()
      .Where(x => x.PrincipalKind == PermissionPrincipalKind.PersonalAccessToken && x.PrincipalId == tokenId)
      .ToListAsync(TestContext.Current.CancellationToken);
    Assert.Empty(surviving);

    var trimLogs = await db.AuthorizationChangeLogs
      .IgnoreQueryFilters()
      .Where(x => x.ActionType == AuthorizationChangeLogActions.CredentialScopeTrim)
      .ToListAsync(TestContext.Current.CancellationToken);
    var entry = Assert.Single(trimLogs);
    Assert.Equal(allowRow.Id, entry.TargetId);
  }

  [Fact]
  public async Task Sweep_WhenOwnerCannotCoverDenyRow_KeepsDenyRow()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput,
      testDatabaseName: $"{Guid.NewGuid()}");

    var tenant = await testApp.Services.CreateTestTenant();
    var userId = (await testApp.Services.CreateTestUser(tenant.Id, $"owner-{Guid.NewGuid():N}@t.local")).Id;
    var tokenId = Guid.NewGuid();
    await SeedPersonalAccessToken(testApp, tokenId, userId);

    // The owner holds nothing, so this Server-scope deny is not owner-covered. It must survive
    // trimming: the manager deliberately accepts it because a deny grants no reach.
    var denyRow = PermissionAssignment.CreateGrant(
      PermissionPrincipalKind.PersonalAccessToken, tokenId,
      PermissionNames.DeviceFileSystemWrite, PermissionScopeKind.Server, null,
      tenant.Id, createdBy: null, effect: PermissionEffect.Deny);
    await SeedAssignment(testApp, denyRow);

    var service = testApp.Services.GetRequiredService<PatScopeTrimBackgroundService>();
    await service.Sweep(TestContext.Current.CancellationToken);

    using var verifyScope = testApp.CreateScope();
    await using var db = verifyScope.ServiceProvider.GetRequiredService<AppDb>();
    var surviving = await db.PermissionAssignments
      .IgnoreQueryFilters()
      .Where(x => x.PrincipalKind == PermissionPrincipalKind.PersonalAccessToken && x.PrincipalId == tokenId)
      .ToListAsync(TestContext.Current.CancellationToken);

    Assert.Single(surviving);
    Assert.Equal(denyRow.Id, surviving[0].Id);
  }

  private static async Task SeedAssignment(TestApp testApp, PermissionAssignment assignment)
  {
    using var scope = testApp.CreateScope();
    await using var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    db.PermissionAssignments.Add(assignment);
    await db.SaveChangesAsync(TestContext.Current.CancellationToken);
  }

  private static async Task SeedPersonalAccessToken(TestApp testApp, Guid tokenId, Guid userId)
  {
    using var scope = testApp.CreateScope();
    await using var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    db.PersonalAccessTokens.Add(new PersonalAccessToken
    {
      Id = tokenId,
      Name = $"pat-{tokenId:N}",
      HashedKey = "test-hashed-key",
      UserId = userId,
      PermissionMode = PersonalAccessTokenPermissionMode.Restricted
    });
    await db.SaveChangesAsync(TestContext.Current.CancellationToken);
  }
}
