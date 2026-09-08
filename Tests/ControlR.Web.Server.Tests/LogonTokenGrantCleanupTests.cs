using ControlR.Web.Server.Data;
using ControlR.Web.Server.Data.Entities;
using ControlR.Web.Server.Services.LogonTokens;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ControlR.Web.Server.Tests;

public class LogonTokenGrantCleanupTests(ITestOutputHelper testOutput)
{
  private readonly ITestOutputHelper _testOutput = testOutput;

  [Fact]
  public async Task CleanOrphanedTokenGrants_ChangeLogEntryWritten()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput,
      testDatabaseName: $"{Guid.NewGuid()}",
      extraConfiguration: new Dictionary<string, string?>
      {
        { "AppOptions:LogonTokenGrantCleanupAfterDays", "21" }
      });

    var backgroundService = testApp.Services.GetRequiredService<LogonTokenCleanupBackgroundService>();
    var tenant = await testApp.Services.CreateTestTenant();
    var device = await testApp.Services.CreateTestDevice(tenant.Id);

    var tokenId = Guid.NewGuid();

    using (var scope = testApp.CreateScope())
    {
      await using var db = scope.ServiceProvider.GetRequiredService<AppDb>();

      db.PermissionAssignments.Add(PermissionAssignment.CreateGrant(
        PermissionPrincipalKind.LogonToken, tokenId,
        PermissionNames.DeviceRead, PermissionScopeKind.Device, device.Id,
        tenant.Id, createdBy: null));
      db.PermissionAssignments.Add(PermissionAssignment.CreateGrant(
        PermissionPrincipalKind.LogonToken, tokenId,
        PermissionNames.DeviceTerminalUse, PermissionScopeKind.Device, device.Id,
        tenant.Id, createdBy: null));
      await db.SaveChangesAsync(TestContext.Current.CancellationToken);

      var rows = await db.PermissionAssignments
        .Where(x => x.PrincipalId == tokenId)
        .ToListAsync(TestContext.Current.CancellationToken);
      foreach (var row in rows)
      {
        row.CreatedAt = testApp.TimeProvider.GetUtcNow().AddDays(-30);
      }

      await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    var removedCount = await backgroundService.CleanOrphanedTokenGrants(TestContext.Current.CancellationToken);
    Assert.Equal(2, removedCount);

    using var verifyScope = testApp.CreateScope();
    var db2 = verifyScope.ServiceProvider.GetRequiredService<AppDb>();
    var changeLog = await db2.AuthorizationChangeLogs
      .Where(x => x.ActionType == AuthorizationChangeLogActions.CredentialScopeRemoved &&
                  x.TargetType == AuthorizationChangeLogTargetTypes.LogonToken &&
                  x.TargetId == tokenId)
      .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

    Assert.NotNull(changeLog);
    Assert.Equal(AuthorizationChangeLogActorTypes.System, changeLog.ActorPrincipalType);
    Assert.Null(changeLog.ActorPrincipalId);
    Assert.Contains("\"scopeCount\":2", changeLog.BeforeJson);
  }

  [Fact]
  public async Task CleanOrphanedTokenGrants_OldRowsDeleted_FreshRowsKept()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput,
      testDatabaseName: $"{Guid.NewGuid()}",
      extraConfiguration: new Dictionary<string, string?>
      {
        { "AppOptions:LogonTokenGrantCleanupAfterDays", "21" }
      });

    var backgroundService = testApp.Services.GetRequiredService<LogonTokenCleanupBackgroundService>();
    var tenant = await testApp.Services.CreateTestTenant();
    var device = await testApp.Services.CreateTestDevice(tenant.Id);

    var oldTokenId = Guid.NewGuid();
    var freshTokenId = Guid.NewGuid();

    using (var scope = testApp.CreateScope())
    {
      await using var db = scope.ServiceProvider.GetRequiredService<AppDb>();

      db.PermissionAssignments.Add(PermissionAssignment.CreateGrant(
        PermissionPrincipalKind.LogonToken, oldTokenId,
        PermissionNames.DeviceRead, PermissionScopeKind.Device, device.Id,
        tenant.Id, createdBy: null));
      await db.SaveChangesAsync(TestContext.Current.CancellationToken);

      var oldRow = await db.PermissionAssignments
        .FirstAsync(x => x.PrincipalId == oldTokenId, TestContext.Current.CancellationToken);
      oldRow.CreatedAt = testApp.TimeProvider.GetUtcNow().AddDays(-30);
      await db.SaveChangesAsync(TestContext.Current.CancellationToken);

      db.PermissionAssignments.Add(PermissionAssignment.CreateGrant(
        PermissionPrincipalKind.LogonToken, freshTokenId,
        PermissionNames.DeviceRead, PermissionScopeKind.Device, device.Id,
        tenant.Id, createdBy: null));
      await db.SaveChangesAsync(TestContext.Current.CancellationToken);

      var freshRow = await db.PermissionAssignments
        .FirstAsync(x => x.PrincipalId == freshTokenId, TestContext.Current.CancellationToken);
      freshRow.CreatedAt = testApp.TimeProvider.GetUtcNow();
      await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    var removedCount = await backgroundService.CleanOrphanedTokenGrants(TestContext.Current.CancellationToken);
    Assert.Equal(1, removedCount);

    using var verifyScope = testApp.CreateScope();
    var db2 = verifyScope.ServiceProvider.GetRequiredService<AppDb>();
    var oldExists = await db2.PermissionAssignments
      .IgnoreQueryFilters()
      .AnyAsync(x => x.PrincipalId == oldTokenId, TestContext.Current.CancellationToken);
    var freshExists = await db2.PermissionAssignments
      .IgnoreQueryFilters()
      .AnyAsync(x => x.PrincipalId == freshTokenId, TestContext.Current.CancellationToken);

    Assert.False(oldExists);
    Assert.True(freshExists);
  }

  [Fact]
  public async Task CleanOrphanedTokenGrants_WhenDisabled_DoesNothing()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput,
      testDatabaseName: $"{Guid.NewGuid()}",
      extraConfiguration: new Dictionary<string, string?>
      {
        { "AppOptions:LogonTokenGrantCleanupAfterDays", "0" }
      });

    var backgroundService = testApp.Services.GetRequiredService<LogonTokenCleanupBackgroundService>();
    var tenant = await testApp.Services.CreateTestTenant();
    var device = await testApp.Services.CreateTestDevice(tenant.Id);

    var tokenId = Guid.NewGuid();

    using (var scope = testApp.CreateScope())
    {
      await using var db = scope.ServiceProvider.GetRequiredService<AppDb>();
      db.PermissionAssignments.Add(PermissionAssignment.CreateGrant(
        PermissionPrincipalKind.LogonToken, tokenId,
        PermissionNames.DeviceRead, PermissionScopeKind.Device, device.Id,
        tenant.Id, createdBy: null));
      await db.SaveChangesAsync(TestContext.Current.CancellationToken);

      var row = await db.PermissionAssignments
        .FirstAsync(x => x.PrincipalId == tokenId, TestContext.Current.CancellationToken);
      row.CreatedAt = testApp.TimeProvider.GetUtcNow().AddDays(-30);
      await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    var removedCount = await backgroundService.CleanOrphanedTokenGrants(TestContext.Current.CancellationToken);
    Assert.Equal(0, removedCount);

    using var verifyScope = testApp.CreateScope();
    var db2 = verifyScope.ServiceProvider.GetRequiredService<AppDb>();
    var exists = await db2.PermissionAssignments
      .IgnoreQueryFilters()
      .AnyAsync(x => x.PrincipalId == tokenId, TestContext.Current.CancellationToken);
    Assert.True(exists);
  }
}
