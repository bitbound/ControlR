using ControlR.Web.Server.Data;
using ControlR.Web.Server.Data.Entities;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace ControlR.Web.Server.Tests;

/// <summary>
/// Verifies the server.alerts.read cleanup deletes only the orphaned grant. The permission was
/// removed from the catalog, so a deployed tenant's rows for it are inert, and the migration exists
/// to stop listing a permission that cannot be granted. The live grant in the same seed proves the
/// DELETE is scoped by name rather than by principal, scope, or tenant.
/// </summary>
public class RemoveOrphanedServerAlertsReadGrantsTests(ITestOutputHelper output)
{
  // The schema immediately before the cleanup. Seeding at this point means the rows exist without
  // the cleanup having run, which is the only state worth testing.
  private const string BeforeCleanupMigration = "20260830122356_AddPermissionAssignmentScopeInvariants";

  private const string OrphanedPermissionName = "server.alerts.read";

  [Fact]
  public async Task RemoveOrphanedGrants_WhenOrphanedAndLiveGrantsExist_DeletesOnlyTheOrphanedGrant()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(
      output,
      useInMemoryDatabase: false,
      applyMigrations: false);

    using var scope = testApp.CreateScope();
    await using var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    var migrator = db.GetInfrastructure().GetRequiredService<IMigrator>();

    await migrator.MigrateAsync(BeforeCleanupMigration, TestContext.Current.CancellationToken);

    var tenant = new Tenant { Id = Guid.NewGuid(), Name = "Alert Grant Cleanup Tenant" };
    db.Tenants.Add(tenant);
    await db.SaveChangesAsync(TestContext.Current.CancellationToken);

    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
    var user = new AppUser
    {
      TenantId = tenant.Id,
      UserName = "alert-grants@test.local",
      Email = "alert-grants@test.local"
    };
    Assert.True((await userManager.CreateAsync(user, "T3stP@ssw0rd!")).Succeeded);

    // Seeded with raw SQL so both rows differ only by permission name, and so a future change to the
    // entity's required members cannot quietly narrow what this test exercises.
    await db.Database.ExecuteSqlRawAsync(
      """
      INSERT INTO "PermissionAssignments"
        ("Id", "Effect", "IsEnabled", "OwningTenantId", "PermissionName", "PrincipalId", "PrincipalKind", "ScopeId", "ScopeKind")
      VALUES
        (gen_random_uuid(), 'Allow', TRUE, {0}, {1}, {2}, 'User', {0}, 'Tenant'),
        (gen_random_uuid(), 'Allow', TRUE, {0}, {3}, {2}, 'User', {0}, 'Tenant');
      """,
      [tenant.Id, OrphanedPermissionName, user.Id, PermissionNames.DeviceRead],
      TestContext.Current.CancellationToken);

    Assert.Equal(2, await db.PermissionAssignments.CountAsync(TestContext.Current.CancellationToken));

    await migrator.MigrateAsync(cancellationToken: TestContext.Current.CancellationToken);

    using var verifyScope = testApp.CreateScope();
    await using var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDb>();

    Assert.Equal(
      0,
      await verifyDb.PermissionAssignments.CountAsync(
        x => x.PermissionName == OrphanedPermissionName,
        TestContext.Current.CancellationToken));

    Assert.Equal(
      1,
      await verifyDb.PermissionAssignments.CountAsync(
        x => x.PermissionName == PermissionNames.DeviceRead,
        TestContext.Current.CancellationToken));
  }
}
