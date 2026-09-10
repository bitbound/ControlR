using ControlR.Web.Server.Data;
using ControlR.Web.Server.Primitives;
using ControlR.Web.Server.Services.ServiceAccounts;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ControlR.Web.Server.Tests;

public class ServiceAccountCredentialPurgeTests(ITestOutputHelper testOutput)
{
  [Fact]
  public async Task PurgeCredentialForServer_RemovesRevokedCredentialAndLogsDeletion()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(testOutput);
    using var scope = testApp.CreateScope();
    var manager = scope.ServiceProvider.GetRequiredService<IServiceAccountManager>();

    var createResult = await manager.CreateForServer(
      "Purge Server SA", null, ServiceAccountAccessMode.Unrestricted, TestContext.Current.CancellationToken);
    Assert.True(createResult.IsSuccess, createResult.Reason);
    var accountId = createResult.Value.Id;

    var credResult = await manager.AddCredentialForServer(
      accountId, "Doomed", null, TestActors.User(), TestContext.Current.CancellationToken);
    Assert.True(credResult.IsSuccess, credResult.Reason);
    var credentialId = credResult.Value.Credential.Id;

    var revokeResult = await manager.RevokeCredentialForServer(
      accountId, credentialId, TestActors.User(), TestContext.Current.CancellationToken);
    Assert.True(revokeResult.IsSuccess, revokeResult.Reason);

    var purgeResult = await manager.PurgeCredentialForServer(
      accountId, credentialId, TestActors.User(), TestContext.Current.CancellationToken);
    Assert.True(purgeResult.IsSuccess, purgeResult.Reason);

    using var verifyScope = testApp.CreateScope();
    await using var db = verifyScope.ServiceProvider.GetRequiredService<AppDb>();
    var stillExists = await db.ServiceAccountCredentials
      .IgnoreQueryFilters()
      .AnyAsync(x => x.Id == credentialId, TestContext.Current.CancellationToken);
    Assert.False(stillExists);

    var log = await db.AuthorizationChangeLogs
      .IgnoreQueryFilters()
      .SingleAsync(
        x => x.ActionType == AuthorizationChangeLogActions.ServiceAccountCredentialDeleted &&
             x.TargetId == credentialId,
        TestContext.Current.CancellationToken);
    Assert.Equal(AuthorizationChangeLogTargetTypes.ServiceAccountCredential, log.TargetType);
  }

  [Fact]
  public async Task PurgeCredentialForServer_WhenCredentialActive_ReturnsBadRequest()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(testOutput);
    using var scope = testApp.CreateScope();
    var manager = scope.ServiceProvider.GetRequiredService<IServiceAccountManager>();

    var createResult = await manager.CreateForServer(
      "Active Cred SA", null, ServiceAccountAccessMode.Unrestricted, TestContext.Current.CancellationToken);
    Assert.True(createResult.IsSuccess, createResult.Reason);

    var credResult = await manager.AddCredentialForServer(
      createResult.Value.Id, "Live", null, TestActors.User(), TestContext.Current.CancellationToken);
    Assert.True(credResult.IsSuccess, credResult.Reason);

    var purgeResult = await manager.PurgeCredentialForServer(
      createResult.Value.Id, credResult.Value.Credential.Id, TestActors.User(), TestContext.Current.CancellationToken);

    Assert.False(purgeResult.IsSuccess);
    Assert.Equal(HttpResultErrorCode.BadRequest, purgeResult.ErrorCode);
  }

  [Fact]
  public async Task PurgeCredentialForServer_WhenCredentialExpired_RemovesIt()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(testOutput);
    using var scope = testApp.CreateScope();
    var manager = scope.ServiceProvider.GetRequiredService<IServiceAccountManager>();

    var createResult = await manager.CreateForServer(
      "Expired Cred SA", null, ServiceAccountAccessMode.Unrestricted, TestContext.Current.CancellationToken);
    Assert.True(createResult.IsSuccess, createResult.Reason);

    var credResult = await manager.AddCredentialForServer(
      createResult.Value.Id,
      "Expiring soon",
      testApp.TimeProvider.GetUtcNow().AddMinutes(5),
      TestActors.User(),
      TestContext.Current.CancellationToken);
    Assert.True(credResult.IsSuccess, credResult.Reason);

    testApp.TimeProvider.Advance(TimeSpan.FromMinutes(10));

    var purgeResult = await manager.PurgeCredentialForServer(
      createResult.Value.Id, credResult.Value.Credential.Id, TestActors.User(), TestContext.Current.CancellationToken);
    Assert.True(purgeResult.IsSuccess, purgeResult.Reason);
  }

  [Fact]
  public async Task PurgeCredentialForTenant_RemovesRevokedCredentialWithTenantAudit()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();

    using var scope = testApp.CreateScope();
    var manager = scope.ServiceProvider.GetRequiredService<IServiceAccountManager>();

    var createResult = await manager.CreateForTenant(
      "Tenant Purge Ok SA", null, tenant.Id, TestActors.User(), TestContext.Current.CancellationToken);
    Assert.True(createResult.IsSuccess, createResult.Reason);

    var credResult = await manager.AddCredentialForTenant(
      createResult.Value.Id, tenant.Id, "Doomed", null, TestActors.User(), TestContext.Current.CancellationToken);
    Assert.True(credResult.IsSuccess, credResult.Reason);

    await manager.RevokeCredentialForTenant(
      createResult.Value.Id, credResult.Value.Credential.Id, tenant.Id, TestActors.User(), TestContext.Current.CancellationToken);

    var purgeResult = await manager.PurgeCredentialForTenant(
      createResult.Value.Id, credResult.Value.Credential.Id, tenant.Id, TestActors.User(), TestContext.Current.CancellationToken);
    Assert.True(purgeResult.IsSuccess, purgeResult.Reason);

    using var verifyScope = testApp.CreateScope();
    await using var db = verifyScope.ServiceProvider.GetRequiredService<AppDb>();
    var log = await db.AuthorizationChangeLogs
      .IgnoreQueryFilters()
      .SingleAsync(
        x => x.ActionType == AuthorizationChangeLogActions.ServiceAccountCredentialDeleted &&
             x.TargetId == credResult.Value.Credential.Id,
        TestContext.Current.CancellationToken);
    Assert.Equal(tenant.Id, log.OwningTenantId);
  }

  [Fact]
  public async Task PurgeCredentialForTenant_WrongTenant_ReturnsNotFound()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    var otherTenant = await testApp.App.Services.CreateTestTenant();

    using var scope = testApp.CreateScope();
    var manager = scope.ServiceProvider.GetRequiredService<IServiceAccountManager>();

    var createResult = await manager.CreateForTenant(
      "Tenant Purge SA", null, tenant.Id, TestActors.User(), TestContext.Current.CancellationToken);
    Assert.True(createResult.IsSuccess, createResult.Reason);

    var credResult = await manager.AddCredentialForTenant(
      createResult.Value.Id, tenant.Id, "Doomed", null, TestActors.User(), TestContext.Current.CancellationToken);
    Assert.True(credResult.IsSuccess, credResult.Reason);

    await manager.RevokeCredentialForTenant(
      createResult.Value.Id, credResult.Value.Credential.Id, tenant.Id, TestActors.User(), TestContext.Current.CancellationToken);

    var purgeResult = await manager.PurgeCredentialForTenant(
      createResult.Value.Id,
      credResult.Value.Credential.Id,
      otherTenant.Id,
      TestActors.User(),
      TestContext.Current.CancellationToken);

    Assert.False(purgeResult.IsSuccess);
    Assert.Equal(HttpResultErrorCode.NotFound, purgeResult.ErrorCode);

    using var verifyScope = testApp.CreateScope();
    await using var db = verifyScope.ServiceProvider.GetRequiredService<AppDb>();
    var stillExists = await db.ServiceAccountCredentials
      .IgnoreQueryFilters()
      .AnyAsync(x => x.Id == credResult.Value.Credential.Id, TestContext.Current.CancellationToken);
    Assert.True(stillExists);
  }
}

public class ServiceAccountCredentialCleanupBackgroundServiceTests(ITestOutputHelper testOutput)
{
  [Fact]
  public async Task CleanDeadCredentials_PurgesCredentialCacheSoValidationFails()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(testOutput,
      extraConfiguration: new Dictionary<string, string?>
      {
        { "AppOptions:ServiceAccountCredentialCleanupAfterDays", "1" }
      });

    var backgroundService = testApp.Services.GetRequiredService<ServiceAccountCredentialCleanupBackgroundService>();
    var manager = testApp.Services.GetRequiredService<IServiceAccountManager>();

    var accountResult = await manager.CreateForServer(
      "Cache Purge SA", null, ServiceAccountAccessMode.Unrestricted, TestContext.Current.CancellationToken);
    Assert.True(accountResult.IsSuccess, accountResult.Reason);

    var credResult = await manager.AddCredentialForServer(
      accountResult.Value.Id, "Stale Cache", null, TestActors.User(), TestContext.Current.CancellationToken);
    Assert.True(credResult.IsSuccess, credResult.Reason);

    // Populate the validation cache with a successful validation, then revoke and clean up.
    var validateResult = await manager.ValidateCredential(
      credResult.Value.PlainTextSecretKey, TestContext.Current.CancellationToken);
    Assert.True(validateResult.IsSuccess, validateResult.Reason);

    await manager.RevokeCredentialForServer(
      accountResult.Value.Id, credResult.Value.Credential.Id, TestActors.User(), TestContext.Current.CancellationToken);

    testApp.TimeProvider.Advance(TimeSpan.FromDays(2));

    var removedCount = await backgroundService.CleanDeadCredentials(TestContext.Current.CancellationToken);
    Assert.Equal(1, removedCount);

    var postPurge = await manager.ValidateCredential(
      credResult.Value.PlainTextSecretKey, TestContext.Current.CancellationToken);
    Assert.False(postPurge.IsSuccess);
    Assert.Equal(HttpResultErrorCode.Unauthorized, postPurge.ErrorCode);
  }

  [Fact]
  public async Task CleanDeadCredentials_RemovesOldRevokedAndExpiredKeepsRecentAndActive()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(testOutput,
      extraConfiguration: new Dictionary<string, string?>
      {
        { "AppOptions:ServiceAccountCredentialCleanupAfterDays", "30" }
      });

    var backgroundService = testApp.Services.GetRequiredService<ServiceAccountCredentialCleanupBackgroundService>();
    var manager = testApp.Services.GetRequiredService<IServiceAccountManager>();

    var accountResult = await manager.CreateForServer(
      "Cleanup SA", null, ServiceAccountAccessMode.Unrestricted, TestContext.Current.CancellationToken);
    Assert.True(accountResult.IsSuccess, accountResult.Reason);
    var accountId = accountResult.Value.Id;

    // Old revoked credential: revoked, then time advanced past the retention window.
    var oldRevoked = await manager.AddCredentialForServer(
      accountId, "Old Revoked", null, TestActors.User(), TestContext.Current.CancellationToken);
    Assert.True(oldRevoked.IsSuccess, oldRevoked.Reason);
    await manager.RevokeCredentialForServer(
      accountId, oldRevoked.Value.Credential.Id, TestActors.User(), TestContext.Current.CancellationToken);

    // Old expired credential: expires, then time advanced past the retention window.
    var oldExpired = await manager.AddCredentialForServer(
      accountId, "Old Expired", testApp.TimeProvider.GetUtcNow().AddDays(1), TestActors.User(), TestContext.Current.CancellationToken);
    Assert.True(oldExpired.IsSuccess, oldExpired.Reason);

    testApp.TimeProvider.Advance(TimeSpan.FromDays(32));

    // Recent revoked credential: revoked after the cutoff.
    var recentRevoked = await manager.AddCredentialForServer(
      accountId, "Recent Revoked", null, TestActors.User(), TestContext.Current.CancellationToken);
    Assert.True(recentRevoked.IsSuccess, recentRevoked.Reason);
    await manager.RevokeCredentialForServer(
      accountId, recentRevoked.Value.Credential.Id, TestActors.User(), TestContext.Current.CancellationToken);

    // Active credential: never revoked, never expired.
    var active = await manager.AddCredentialForServer(
      accountId, "Active", null, TestActors.User(), TestContext.Current.CancellationToken);
    Assert.True(active.IsSuccess, active.Reason);

    var removedCount = await backgroundService.CleanDeadCredentials(TestContext.Current.CancellationToken);

    Assert.Equal(2, removedCount);

    using var scope = testApp.CreateScope();
    await using var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    var remaining = await db.ServiceAccountCredentials
      .IgnoreQueryFilters()
      .Where(x => x.ServiceAccountId == accountId)
      .Select(x => x.Name)
      .ToListAsync(TestContext.Current.CancellationToken);

    Assert.Contains("Recent Revoked", remaining);
    Assert.Contains("Active", remaining);
    Assert.DoesNotContain("Old Revoked", remaining);
    Assert.DoesNotContain("Old Expired", remaining);

    var deleteLogs = await db.AuthorizationChangeLogs
      .IgnoreQueryFilters()
      .Where(x => x.ActionType == AuthorizationChangeLogActions.ServiceAccountCredentialDeleted)
      .ToListAsync(TestContext.Current.CancellationToken);
    Assert.Equal(2, deleteLogs.Count);
    Assert.All(deleteLogs, log => Assert.Equal(AuthorizationChangeLogActorTypes.System, log.ActorPrincipalType));
  }

  [Fact]
  public async Task CleanDeadCredentials_WhenCleanupDisabled_DoesNothing()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(testOutput,
      extraConfiguration: new Dictionary<string, string?>
      {
        { "AppOptions:ServiceAccountCredentialCleanupAfterDays", "0" }
      });

    var backgroundService = testApp.Services.GetRequiredService<ServiceAccountCredentialCleanupBackgroundService>();
    var manager = testApp.Services.GetRequiredService<IServiceAccountManager>();

    var accountResult = await manager.CreateForServer(
      "Disabled Cleanup SA", null, ServiceAccountAccessMode.Unrestricted, TestContext.Current.CancellationToken);
    Assert.True(accountResult.IsSuccess, accountResult.Reason);

    var credResult = await manager.AddCredentialForServer(
      accountResult.Value.Id, "Old Revoked", null, TestActors.User(), TestContext.Current.CancellationToken);
    Assert.True(credResult.IsSuccess, credResult.Reason);
    await manager.RevokeCredentialForServer(
      accountResult.Value.Id, credResult.Value.Credential.Id, TestActors.User(), TestContext.Current.CancellationToken);

    testApp.TimeProvider.Advance(TimeSpan.FromDays(365));

    var removedCount = await backgroundService.CleanDeadCredentials(TestContext.Current.CancellationToken);

    Assert.Equal(0, removedCount);

    using var scope = testApp.CreateScope();
    await using var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    var stillExists = await db.ServiceAccountCredentials
      .IgnoreQueryFilters()
      .AnyAsync(x => x.Id == credResult.Value.Credential.Id, TestContext.Current.CancellationToken);
    Assert.True(stillExists);
  }
}
