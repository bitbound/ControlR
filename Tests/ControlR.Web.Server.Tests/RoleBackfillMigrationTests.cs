using ControlR.Web.Server.Data;
using ControlR.Web.Server.Data.Entities;
using ControlR.Web.Server.Data.Enums;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace ControlR.Web.Server.Tests;

/// <summary>
/// Verifies the Remove_Roles migration backfills each user's legacy role memberships into
/// permission assignments before dropping the role tables, so existing users keep their access
/// when upgrading across the role-to-permission cutover.
/// </summary>
public class RoleBackfillMigrationTests(ITestOutputHelper output)
{
  private const string PreRemoveRolesMigration = "20260729192550_Update_EntityBase";

  /// <summary>
  /// Regression guard: the role→permission backfill must be a complete,
  /// correctly-scoped superset of every preset. The Phase2 migration originally omitted
  /// Server Administrator ×4 (ServerPermissionsRead, ServerPermissionsWrite,
  /// TenantPermissionsRead, TenantAuthorizationLogsRead) and Device Superuser ×2
  /// (DeviceOverviewRead, DeviceVncRelayConnect), leaving upgraded legacy users
  /// permanently under-privileged. TenantPermissionsRead / TenantAuthorizationLogsRead
  /// must also be backfilled to Tenant scope (not Server), matching PermissionCatalog.
  /// </summary>
  [Fact]
  public async Task RemoveRoles_BackfillsFullPresetCoverage_WithCorrectScopes()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(
      output,
      useInMemoryDatabase: false,
      applyMigrations: false);

    using var scope = testApp.CreateScope();
    await using var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    var migrator = db.GetInfrastructure().GetRequiredService<IMigrator>();

    await migrator.MigrateAsync(PreRemoveRolesMigration, TestContext.Current.CancellationToken);

    var tenant = new Tenant { Id = Guid.NewGuid(), Name = "Full Coverage Tenant" };
    db.Tenants.Add(tenant);
    await db.SaveChangesAsync(TestContext.Current.CancellationToken);

    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
    var user = new AppUser
    {
      TenantId = tenant.Id,
      UserName = "coverage@test.local",
      Email = "coverage@test.local"
    };
    var createResult = await userManager.CreateAsync(user, "T3stP@ssw0rd!");
    Assert.True(createResult.Succeeded);

    // Grant every backfilled role so the full superset is exercised.
    await db.Database.ExecuteSqlRawAsync(
      """
      INSERT INTO "AspNetUserRoles" ("UserId", "RoleId")
      SELECT {0}, "Id" FROM "AspNetRoles"
      WHERE "Name" IN ('Server Administrator', 'Tenant Administrator', 'Device Superuser', 'Installer Key Manager', 'Agent Installer');
      """,
      user.Id);

    await migrator.MigrateAsync(cancellationToken: TestContext.Current.CancellationToken);

    using var verifyScope = testApp.CreateScope();
    await using var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDb>();

    var all = await verifyDb.PermissionAssignments
      .Where(x => x.PrincipalId == user.Id)
      .ToListAsync(TestContext.Current.CancellationToken);

    var serverRoleRows = all.Where(x => x.ScopeKind == PermissionScopeKind.Server).Select(x => x.PermissionName).ToHashSet();
    var tenantRoleRows = all.Where(x => x.ScopeKind == PermissionScopeKind.Tenant).Select(x => x.PermissionName).ToHashSet();

    // Server-scoped subset of the Server Administrator preset (server-only permissions).
    var expectedServer = new[]
    {
      PermissionNames.ServerAuthorizationLogsRead,
      PermissionNames.ServerPermissionsRead,
      PermissionNames.ServerPermissionsWrite,
      PermissionNames.ServerSettingsWrite,
      PermissionNames.ServerTenantsRead,
      PermissionNames.ServerTenantsWrite,
      PermissionNames.ServerTelemetryRead,
      PermissionNames.ServerServiceAccountsRead,
      PermissionNames.ServerServiceAccountsWrite,
      PermissionNames.ServerServiceAccountsRotateCredentials,
    };
    Assert.Empty(expectedServer.Except(serverRoleRows));

    // Tenant-scoped permissions from Server Administrator + Tenant Administrator presets.
    var expectedTenant = new[]
    {
      PermissionNames.TenantPermissionsRead,
      PermissionNames.TenantAuthorizationLogsRead,
      PermissionNames.TenantPermissionsWrite,
      PermissionNames.TenantPermissionsDeny,
      PermissionNames.TenantUsersWrite,
    };
    Assert.Empty(expectedTenant.Except(tenantRoleRows));

    // Device Superuser preset — all present and tenant-scoped (broadest legal scope is Tenant).
    var expectedDevice = new[]
    {
      PermissionNames.DeviceRead,
      PermissionNames.DeviceDelete,
      PermissionNames.DeviceOverviewRead,
      PermissionNames.DeviceVncRelayConnect,
      PermissionNames.DeviceRemoteControlConnect,
      PermissionNames.DeviceFileSystemRead,
      PermissionNames.DeviceTerminalUse,
      PermissionNames.DeviceAgentUpdate,
    };
    Assert.Empty(expectedDevice.Except(all.Where(x => x.ScopeKind == PermissionScopeKind.Tenant).Select(x => x.PermissionName)));

    foreach (var perm in expectedTenant)
    {
      var row = all.First(x => x.PermissionName == perm);
      Assert.Equal(PermissionScopeKind.Tenant, row.ScopeKind);
      Assert.Equal(tenant.Id, row.ScopeId);
      Assert.Equal(tenant.Id, row.OwningTenantId);
    }
  }

  [Fact]
  public async Task RemoveRoles_BackfillsRoleMembershipsIntoPermissionAssignments()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(
      output,
      useInMemoryDatabase: false,
      applyMigrations: false);

    using var scope = testApp.CreateScope();
    await using var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    var migrator = db.GetInfrastructure().GetRequiredService<IMigrator>();

    // Stage the database at the migration just before Remove_Roles: the role tables (with the
    // seeded built-in roles) and the PermissionAssignments table both exist.
    await migrator.MigrateAsync(PreRemoveRolesMigration, TestContext.Current.CancellationToken);

    var tenant = new Tenant { Id = Guid.NewGuid(), Name = "Backfill Tenant" };
    db.Tenants.Add(tenant);
    await db.SaveChangesAsync(TestContext.Current.CancellationToken);

    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
    var user = new AppUser
    {
      TenantId = tenant.Id,
      UserName = "backfill@test.local",
      Email = "backfill@test.local"
    };
    var createResult = await userManager.CreateAsync(user, "T3stP@ssw0rd!");
    Assert.True(createResult.Succeeded);
    var agentInstallerOnlyUser = new AppUser
    {
      TenantId = tenant.Id,
      UserName = "agent-installer@test.local",
      Email = "agent-installer@test.local"
    };
    var agentInstallerCreateResult = await userManager.CreateAsync(
      agentInstallerOnlyUser,
      "T3stP@ssw0rd!");
    Assert.True(agentInstallerCreateResult.Succeeded);

    // Give the user three legacy roles. Installer Key Manager and Tenant Administrator
    // overlap on agent.install and installer-key permissions.
    await db.Database.ExecuteSqlRawAsync(
      """
      INSERT INTO "AspNetUserRoles" ("UserId", "RoleId")
      SELECT {0}, "Id" FROM "AspNetRoles" WHERE "Name" IN ('Server Administrator', 'Tenant Administrator', 'Installer Key Manager');
      """,
      user.Id);
    await db.Database.ExecuteSqlRawAsync(
      """
      INSERT INTO "AspNetUserRoles" ("UserId", "RoleId")
      SELECT {0}, "Id" FROM "AspNetRoles" WHERE "Name" = 'Agent Installer';
      """,
      agentInstallerOnlyUser.Id);

    // Apply Remove_Roles: backfills permission assignments, then drops the role tables.
    await migrator.MigrateAsync(cancellationToken: TestContext.Current.CancellationToken);

    using var verifyScope = testApp.CreateScope();
    await using var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDb>();

    // Server Administrator preset permissions — server-scoped, no ScopeId, no OwningTenantId.
    var serverAdminPermissions = await verifyDb.PermissionAssignments
      .Where(x => x.PrincipalId == user.Id &&
                  x.ScopeKind == PermissionScopeKind.Server &&
                 (x.PermissionName == PermissionNames.ServerTenantsWrite ||
                  x.PermissionName == PermissionNames.ServerTelemetryRead ||
                  x.PermissionName == PermissionNames.ServerServiceAccountsWrite))
      .ToListAsync(TestContext.Current.CancellationToken);

    Assert.NotEmpty(serverAdminPermissions);
    foreach (var perm in serverAdminPermissions)
    {
      Assert.Null(perm.ScopeId);
      Assert.Null(perm.OwningTenantId);
      Assert.Equal(PermissionScopeKind.Server, perm.ScopeKind);
    }

    // Tenant Administrator preset permissions — tenant-scoped with ScopeId = tenantId,
    // which tenant-scope evaluation requires for tenant-scoped assignments.
    var tenantAdminPermissions = await verifyDb.PermissionAssignments
      .Where(x => x.PrincipalId == user.Id &&
                 (x.PermissionName == PermissionNames.TenantSettingsWrite ||
                  x.PermissionName == PermissionNames.TenantUsersRead))
      .ToListAsync(TestContext.Current.CancellationToken);

    Assert.NotEmpty(tenantAdminPermissions);
    foreach (var perm in tenantAdminPermissions)
    {
      Assert.Equal(tenant.Id, perm.ScopeId);
      Assert.Equal(PermissionScopeKind.Tenant, perm.ScopeKind);
      Assert.Equal(tenant.Id, perm.OwningTenantId);
    }

    // Installer Key Manager permissions are tenant-scoped, even when the user also has
    // Tenant Administrator, and duplicate permissions collapse to one assignment.
    var installerKeyPermissions = await verifyDb.PermissionAssignments
      .Where(x => x.PrincipalId == user.Id &&
                 (x.PermissionName == PermissionNames.InstallerKeyRead ||
                  x.PermissionName == PermissionNames.InstallerKeyWrite ||
                  x.PermissionName == PermissionNames.AgentInstall))
      .ToListAsync(TestContext.Current.CancellationToken);

    Assert.Equal(3, installerKeyPermissions.Count);
    foreach (var permission in installerKeyPermissions)
    {
      Assert.Equal(tenant.Id, permission.ScopeId);
      Assert.Equal(PermissionScopeKind.Tenant, permission.ScopeKind);
      Assert.Equal(tenant.Id, permission.OwningTenantId);
    }

    var agentInstallerPermissions = await verifyDb.PermissionAssignments
      .Where(x => x.PrincipalId == agentInstallerOnlyUser.Id)
      .Select(x => x.PermissionName)
      .ToListAsync(TestContext.Current.CancellationToken);

    // The role backfill plus the all-users self-service PAT baseline (released behavior:
    // any authenticated user could manage their own tokens). Expected set is derived from
    // the live preset so that growing SelfService fails this test until the migration's
    // hard-coded SQL is updated in step. Retire this coupling once Permissions_Phase2 ships.
    Assert.Equal(
      PermissionPresets
        .GetPermissions(PermissionPresets.AgentInstaller)
        .Concat(PermissionPresets.GetPermissions(PermissionPresets.SelfService))
        .Order()
        .ToArray(),
      agentInstallerPermissions.Order());
  }

  /// <summary>
  /// The self-service baseline backfill grants every non-external user the tenant-scoped
  /// self permissions (pre-permissions, the self-PAT endpoints were open to any authenticated
  /// user), skips external/guest accounts, and does not duplicate rows for users whose role
  /// backfill already granted them. Expected permissions are derived from the live
  /// <see cref="PermissionPresets.SelfService"/> preset so the unreleased migration's SQL and
  /// the preset cannot silently drift apart.
  /// </summary>
  [Fact]
  public async Task RemoveRoles_BackfillsSelfServicePatPermissions_ForAllNonExternalUsers()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(
      output,
      useInMemoryDatabase: false,
      applyMigrations: false);

    using var scope = testApp.CreateScope();
    await using var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    var migrator = db.GetInfrastructure().GetRequiredService<IMigrator>();

    await migrator.MigrateAsync(PreRemoveRolesMigration, TestContext.Current.CancellationToken);

    var tenant = new Tenant { Id = Guid.NewGuid(), Name = "Self Service Tenant" };
    db.Tenants.Add(tenant);
    await db.SaveChangesAsync(TestContext.Current.CancellationToken);

    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
    var plainUser = new AppUser
    {
      TenantId = tenant.Id,
      UserName = "plain@test.local",
      Email = "plain@test.local"
    };
    Assert.True((await userManager.CreateAsync(plainUser, "T3stP@ssw0rd!")).Succeeded);

    var adminUser = new AppUser
    {
      TenantId = tenant.Id,
      UserName = "admin@test.local",
      Email = "admin@test.local"
    };
    Assert.True((await userManager.CreateAsync(adminUser, "T3stP@ssw0rd!")).Succeeded);

    var externalUser = new AppUser
    {
      TenantId = tenant.Id,
      UserName = "ext-guest",
      Email = "ext-guest@controlr.local",
      AccountType = AccountType.ExternalUser
    };
    Assert.True((await userManager.CreateAsync(externalUser, "T3stP@ssw0rd!")).Succeeded);

    // The admin user already receives the self permissions from the Tenant Administrator
    // role backfill. The NOT EXISTS guard must not duplicate them.
    await db.Database.ExecuteSqlRawAsync(
      """
      INSERT INTO "AspNetUserRoles" ("UserId", "RoleId")
      SELECT {0}, "Id" FROM "AspNetRoles" WHERE "Name" = 'Tenant Administrator';
      """, adminUser.Id);

    await migrator.MigrateAsync(cancellationToken: TestContext.Current.CancellationToken);

    using var verifyScope = testApp.CreateScope();
    await using var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDb>();

    var selfPermissions = PermissionPresets
      .GetPermissions(PermissionPresets.SelfService)
      .ToArray();

    var plainRows = await verifyDb.PermissionAssignments
      .Where(x => x.PrincipalId == plainUser.Id && selfPermissions.Contains(x.PermissionName))
      .ToListAsync(TestContext.Current.CancellationToken);
    Assert.Equal(selfPermissions.Length, plainRows.Count);
    foreach (var row in plainRows)
    {
      Assert.Equal(PermissionPrincipalKind.User, row.PrincipalKind);
      Assert.Equal(PermissionEffect.Allow, row.Effect);
      Assert.Equal(PermissionScopeKind.Tenant, row.ScopeKind);
      Assert.Equal(tenant.Id, row.ScopeId);
      Assert.Equal(tenant.Id, row.OwningTenantId);
      Assert.True(row.IsEnabled);
    }

    var adminRows = await verifyDb.PermissionAssignments
      .Where(x => x.PrincipalId == adminUser.Id && selfPermissions.Contains(x.PermissionName))
      .ToListAsync(TestContext.Current.CancellationToken);
    Assert.Equal(selfPermissions.Length, adminRows.Count);

    var externalRows = await verifyDb.PermissionAssignments
      .Where(x => x.PrincipalId == externalUser.Id)
      .CountAsync(TestContext.Current.CancellationToken);
    Assert.Equal(0, externalRows);
  }
}
