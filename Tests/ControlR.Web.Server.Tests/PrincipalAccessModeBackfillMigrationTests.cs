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
/// Verifies the AddPrincipalAccessModes backfill preserves each existing principal's prior,
/// inferred behavior. The two inference rules differed: PATs inherited owner permissions only
/// when they had no ENABLED scope rows (the loader's patRules query filtered IsEnabled), while
/// server service accounts bypassed only when they had NO assignment rows at all (no IsEnabled
/// filter). This test seeds all row combinations per principal type before the migration and
/// asserts each principal lands on the mode that matches its old behavior.
/// </summary>
public class PrincipalAccessModeBackfillMigrationTests(ITestOutputHelper output)
{
  // The schema snapshot this backfill's assertions depend on. This must match the migration
  // immediately before AddPrincipalAccessModes. The raw-SQL seeds target this interim column set.
  // If that migration (ControlR.Web.Server.Data.Migrations.Permissions_Phase2) is ever squashed
  // or renamed, keep this constant pointing at the same schema boundary.
  private const string PreAccessModesMigration = "20260813192551_Permissions_Phase2";

  [Fact]
  public async Task AddPrincipalAccessModes_Backfill_PreservesPriorInferredBehavior()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(
      output,
      useInMemoryDatabase: false,
      applyMigrations: false);

    using var scope = testApp.CreateScope();
    await using var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    var migrator = db.GetInfrastructure().GetRequiredService<IMigrator>();

    // Stop right before the access-modes migration so the seeded rows exist without the columns.
    await migrator.MigrateAsync(PreAccessModesMigration, TestContext.Current.CancellationToken);

    var tenant = new Tenant { Id = Guid.NewGuid(), Name = "Backfill Tenant" };
    db.Tenants.Add(tenant);
    await db.SaveChangesAsync(TestContext.Current.CancellationToken);

    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
    var user = new AppUser
    {
      TenantId = tenant.Id,
      UserName = "pat-owner@test.local",
      Email = "pat-owner@test.local"
    };
    var createUserResult = await userManager.CreateAsync(user, "T3stP@ssw0rd!");
    Assert.True(createUserResult.Succeeded);

    // PAT with no scope rows: old loader fell back to owner rules -> InheritOwner.
    var patNoRows = Guid.NewGuid();
    // PAT with an enabled scope row: old loader used patRules -> Restricted.
    var patEnabledRow = Guid.NewGuid();
    // PAT with a disabled-only row: the loader's enabled-only query saw zero rows -> InheritOwner.
    var patDisabledRow = Guid.NewGuid();

    // Server account with no rows: old loader bypassed -> Unrestricted.
    var serverNoRows = Guid.NewGuid();
    // Server account with an enabled row: no bypass -> Restricted.
    var serverEnabledRow = Guid.NewGuid();
    // Server account with a disabled-only row: the old absence check ignored IsEnabled -> Restricted.
    var serverDisabledRow = Guid.NewGuid();
    // Tenant account with no rows: never governed by AccessMode, never bypassed -> stays default.
    var tenantNoRows = Guid.NewGuid();

    // The schema at this migration point lacks the mode columns and the model-built EF inserts
    // would fail, so seed with raw SQL matching the interim schema.
    await db.Database.ExecuteSqlRawAsync(
      """
      INSERT INTO "PersonalAccessTokens" ("Id", "Name", "HashedKey", "UserId")
      VALUES
        ({0}, 'pat-no-rows', 'hash-a', {1}),
        ({2}, 'pat-enabled-row', 'hash-b', {1}),
        ({3}, 'pat-disabled-row', 'hash-c', {1});
      """,
      [patNoRows, user.Id, patEnabledRow, patDisabledRow],
      TestContext.Current.CancellationToken);

    await db.Database.ExecuteSqlRawAsync(
      """
      INSERT INTO "ServiceAccounts" ("Id", "Kind", "TenantId", "Name", "IsEnabled")
      VALUES
        ({0}, 'Server', NULL, 'server-no-rows', TRUE),
        ({1}, 'Server', NULL, 'server-enabled-row', TRUE),
        ({2}, 'Server', NULL, 'server-disabled-row', TRUE),
        ({3}, 'Tenant', {4}, 'tenant-no-rows', TRUE);
      """,
      [serverNoRows, serverEnabledRow, serverDisabledRow, tenantNoRows, tenant.Id],
      TestContext.Current.CancellationToken);

    await db.Database.ExecuteSqlRawAsync(
      """
      INSERT INTO "PermissionAssignments"
        ("Id", "Effect", "IsEnabled", "OwningTenantId", "PermissionName", "PrincipalId", "PrincipalKind", "ScopeId", "ScopeKind")
      VALUES
        (gen_random_uuid(), 'Allow', TRUE, {0}, {1}, {2}, 'PersonalAccessToken', {0}, 'Tenant'),
        (gen_random_uuid(), 'Allow', FALSE, {0}, {1}, {3}, 'PersonalAccessToken', {0}, 'Tenant'),
        (gen_random_uuid(), 'Allow', TRUE, {0}, {1}, {4}, 'ServiceAccount', {0}, 'Tenant'),
        (gen_random_uuid(), 'Allow', FALSE, {0}, {1}, {5}, 'ServiceAccount', {0}, 'Tenant');
      """,
      [tenant.Id, PermissionNames.DeviceRead, patEnabledRow, patDisabledRow, serverEnabledRow, serverDisabledRow],
      TestContext.Current.CancellationToken);

    // Apply the access-modes migration (columns + backfill).
    await migrator.MigrateAsync(cancellationToken: TestContext.Current.CancellationToken);

    using var verifyScope = testApp.CreateScope();
    await using var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDb>();

    var patModes = await verifyDb.PersonalAccessTokens
      .IgnoreQueryFilters()
      .AsNoTracking()
      .Where(token => token.Id == patNoRows || token.Id == patEnabledRow || token.Id == patDisabledRow)
      .ToDictionaryAsync(token => token.Id, token => token.PermissionMode, TestContext.Current.CancellationToken);

    Assert.Equal(PersonalAccessTokenPermissionMode.InheritOwner, patModes[patNoRows]);
    Assert.Equal(PersonalAccessTokenPermissionMode.Restricted, patModes[patEnabledRow]);
    Assert.Equal(PersonalAccessTokenPermissionMode.InheritOwner, patModes[patDisabledRow]);

    var accountModes = await verifyDb.ServiceAccounts
      .IgnoreQueryFilters()
      .AsNoTracking()
      .Where(account =>
        account.Id == serverNoRows || account.Id == serverEnabledRow ||
        account.Id == serverDisabledRow || account.Id == tenantNoRows)
      .ToDictionaryAsync(account => account.Id, account => account.AccessMode, TestContext.Current.CancellationToken);

    Assert.Equal(ServiceAccountAccessMode.Unrestricted, accountModes[serverNoRows]);
    Assert.Equal(ServiceAccountAccessMode.Restricted, accountModes[serverEnabledRow]);
    Assert.Equal(ServiceAccountAccessMode.Restricted, accountModes[serverDisabledRow]);
    Assert.Equal(ServiceAccountAccessMode.Restricted, accountModes[tenantNoRows]);
  }
}
