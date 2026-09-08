using ControlR.Web.Server.Authz.Permissions;
using ControlR.Web.Server.Data;
using ControlR.Web.Server.Data.Entities;
using ControlR.Web.Server.Services.Authorization;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ControlR.Web.Server.Tests;

/// <summary>
/// Persistence invariants (P2-3). Verifies that the database-level CHECK constraints on
/// <see cref="PermissionAssignment"/> reject invalid scope/ownership combinations at the
/// DB boundary (bypassing the manager), and that the decision evaluator fails closed on
/// unknown permissions and illegal scope kinds. The DB tests require a real PostgreSQL
/// instance (testcontainers) because the constraints only exist there.
/// </summary>
public class PermissionPersistenceInvariantsTests(ITestOutputHelper testOutput)
{
  private readonly ITestOutputHelper _testOutput = testOutput;

  [Fact]
  public async Task DbBoundary_DeviceScopeWithoutScopeId_Rejected()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput, useInMemoryDatabase: false);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    await using var appDb = services.GetRequiredService<AppDb>();
    var tenant = await services.CreateTestTenant();

    var ex = await Assert.ThrowsAsync<PostgresException>(
      () => InsertRaw(appDb,
        scopeKind: PermissionScopeKind.Device,
        scopeId: null,
        owningTenantId: tenant.Id,
        cancellationToken: TestContext.Current.CancellationToken));

    Assert.Contains("CA_PermissionAssignments_ScopeKind_ScopeId", ex.Message);
  }

  [Fact]
  public async Task DbBoundary_ServerScopeWithOwningTenantId_Rejected()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput, useInMemoryDatabase: false);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    await using var appDb = services.GetRequiredService<AppDb>();
    var tenant = await services.CreateTestTenant();

    var ex = await Assert.ThrowsAsync<PostgresException>(
      () => InsertRaw(appDb,
        scopeKind: PermissionScopeKind.Server,
        scopeId: null,
        owningTenantId: tenant.Id,
        cancellationToken: TestContext.Current.CancellationToken));

    Assert.Contains("CA_PermissionAssignments_Server_NullScope", ex.Message);
  }

  // ---------- DB boundary: check constraints reject invalid persisted rows ----------

  [Fact]
  public async Task DbBoundary_ServerScopeWithScopeId_Rejected()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput, useInMemoryDatabase: false);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    await using var appDb = services.GetRequiredService<AppDb>();
    var tenant = await services.CreateTestTenant();

    var ex = await Assert.ThrowsAsync<PostgresException>(
      () => InsertRaw(appDb,
        scopeKind: PermissionScopeKind.Server,
        scopeId: tenant.Id,
        owningTenantId: null,
        cancellationToken: TestContext.Current.CancellationToken));

    Assert.Contains("CA_PermissionAssignments_Server_NullScope", ex.Message);
  }

  [Fact]
  public async Task DbBoundary_TenantScopeWithoutOwningTenantId_Rejected()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput, useInMemoryDatabase: false);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    await using var appDb = services.GetRequiredService<AppDb>();
    var tenant = await services.CreateTestTenant();

    var ex = await Assert.ThrowsAsync<PostgresException>(
      () => InsertRaw(appDb,
        scopeKind: PermissionScopeKind.Tenant,
        scopeId: tenant.Id,
        owningTenantId: null,
        cancellationToken: TestContext.Current.CancellationToken));

    Assert.Contains("CA_PermissionAssignments_NonServer_OwningTenant", ex.Message);
  }

  [Fact]
  public async Task DbBoundary_TenantScopeWithoutScopeId_Rejected()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput, useInMemoryDatabase: false);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    await using var appDb = services.GetRequiredService<AppDb>();
    var tenant = await services.CreateTestTenant();

    var ex = await Assert.ThrowsAsync<PostgresException>(
      () => InsertRaw(appDb,
        scopeKind: PermissionScopeKind.Tenant,
        scopeId: null,
        owningTenantId: tenant.Id,
        cancellationToken: TestContext.Current.CancellationToken));

    Assert.Contains("CA_PermissionAssignments_ScopeKind_ScopeId", ex.Message);
  }

  [Fact]
  public async Task DbBoundary_ValidServerScope_Accepted()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput, useInMemoryDatabase: false);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    await using var appDb = services.GetRequiredService<AppDb>();

    // No exception: a server-scoped row carries null ScopeId and null OwningTenantId.
    await InsertRaw(appDb,
      scopeKind: PermissionScopeKind.Server,
      scopeId: null,
      owningTenantId: null,
      cancellationToken: TestContext.Current.CancellationToken);

    var count = await appDb.PermissionAssignments
      .IgnoreQueryFilters()
      .CountAsync(x => x.ScopeKind == PermissionScopeKind.Server &&
                       x.PermissionName == PermissionNames.ServerTenantsWrite &&
                       x.ScopeId == null && x.OwningTenantId == null,
      TestContext.Current.CancellationToken);
    Assert.True(count >= 1);
  }

  [Fact]
  public async Task DbBoundary_ValidTenantScope_Accepted()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput, useInMemoryDatabase: false);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    await using var appDb = services.GetRequiredService<AppDb>();
    var tenant = await services.CreateTestTenant();

    // No exception: a tenant-scoped row carries a non-null ScopeId and OwningTenantId.
    await InsertRaw(appDb,
      permissionName: PermissionNames.TenantRead,
      scopeKind: PermissionScopeKind.Tenant,
      scopeId: tenant.Id,
      owningTenantId: tenant.Id,
      cancellationToken: TestContext.Current.CancellationToken);

    var count = await appDb.PermissionAssignments
      .IgnoreQueryFilters()
      .CountAsync(x => x.ScopeKind == PermissionScopeKind.Tenant &&
                       x.PermissionName == PermissionNames.TenantRead &&
                       x.ScopeId == tenant.Id && x.OwningTenantId == tenant.Id,
      TestContext.Current.CancellationToken);
    Assert.True(count >= 1);
  }

  [Fact]
  public async Task Evaluator_AllowAtIllegalScopeKind_FailsClosed()
  {
    // AgentInstall is tenant-only; an allow at Device scope is illegal and must not authorize.
    // The row is CHECK-constraint-valid (has ScopeId + OwningTenantId) but per-permission
    // scope legality is enforced at write time and in the evaluator, not at the DB boundary.
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    var user = await testApp.App.Services.CreateTestUser(tenant.Id);
    var device = await testApp.App.Services.CreateTestDevice(tenant.Id);

    await SeedAssignment(testApp, new PermissionAssignment
    {
      PrincipalKind = PermissionPrincipalKind.User,
      PrincipalId = user.Id,
      PermissionName = PermissionNames.AgentInstall,
      Effect = PermissionEffect.Allow,
      ScopeKind = PermissionScopeKind.Device,
      ScopeId = device.Id,
      OwningTenantId = tenant.Id,
      IsEnabled = true
    });

    var evaluator = GetEvaluator(testApp);
    var principal = CreateUserPrincipal(user.Id, tenant.Id);
    var resource = new ResourceDescriptor(PermissionScopeKind.Device, device.Id, tenant.Id);

    var result = await evaluator.Evaluate(
      principal, PermissionNames.AgentInstall, resource, TestContext.Current.CancellationToken);

    Assert.False(result.Allowed);
  }

  [Fact]
  public async Task Evaluator_DenyAtIllegalScopeKind_IsHonored_FailsClosed()
  {
    // A deny at a scope the permission does not permit must still be honored (never dropped),
    // because dropping a deny would fail open.
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    var user = await testApp.App.Services.CreateTestUser(tenant.Id);
    var device = await testApp.App.Services.CreateTestDevice(tenant.Id);

    // A legal tenant-scoped allow so there is something to deny.
    await SeedAssignment(testApp, new PermissionAssignment
    {
      PrincipalKind = PermissionPrincipalKind.User,
      PrincipalId = user.Id,
      PermissionName = PermissionNames.AgentInstall,
      Effect = PermissionEffect.Allow,
      ScopeKind = PermissionScopeKind.Tenant,
      ScopeId = tenant.Id,
      OwningTenantId = tenant.Id,
      IsEnabled = true
    });

    // An illegal device-scoped deny on a tenant-only permission.
    await SeedAssignment(testApp, new PermissionAssignment
    {
      PrincipalKind = PermissionPrincipalKind.User,
      PrincipalId = user.Id,
      PermissionName = PermissionNames.AgentInstall,
      Effect = PermissionEffect.Deny,
      ScopeKind = PermissionScopeKind.Device,
      ScopeId = device.Id,
      OwningTenantId = tenant.Id,
      IsEnabled = true
    });

    var evaluator = GetEvaluator(testApp);
    var principal = CreateUserPrincipal(user.Id, tenant.Id);
    var resource = new ResourceDescriptor(PermissionScopeKind.Device, device.Id, tenant.Id);

    var result = await evaluator.Evaluate(
      principal, PermissionNames.AgentInstall, resource, TestContext.Current.CancellationToken);

    Assert.False(result.Allowed);
    Assert.Contains("deny", result.DenialReason, StringComparison.OrdinalIgnoreCase);
  }

  // ---------- Evaluator fails closed on unknown / illegal rules ----------

  [Fact]
  public async Task Evaluator_UnknownPermission_FailsClosed()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    var user = await testApp.App.Services.CreateTestUser(tenant.Id);

    var evaluator = GetEvaluator(testApp);
    var principal = CreateUserPrincipal(user.Id, tenant.Id);
    var resource = new ResourceDescriptor(PermissionScopeKind.Tenant, tenant.Id, tenant.Id);

    var result = await evaluator.Evaluate(
      principal, "TotallyUnknownPermission", resource, TestContext.Current.CancellationToken);

    Assert.False(result.Allowed);
    Assert.Contains("Unknown permission", result.DenialReason);
  }

  private static PrincipalDescriptor CreateUserPrincipal(Guid userId, Guid tenantId)
  {
    return new PrincipalDescriptor(
      PrincipalType.User,
      userId,
      tenantId,
      AuthMethod: "cookie");
  }

  private static IPermissionEvaluator GetEvaluator(TestApp testApp)
  {
    return testApp.App.Services.GetRequiredService<IPermissionEvaluator>();
  }

  // ---------- helpers ----------

  private static async Task InsertRaw(
    AppDb appDb,
    PermissionScopeKind scopeKind,
    Guid? scopeId,
    Guid? owningTenantId,
    CancellationToken cancellationToken,
    string? permissionName = null)
  {
    var connStr = appDb.Database.GetConnectionString()
      ?? throw new InvalidOperationException("No connection string");

    await using var conn = new NpgsqlConnection(connStr);
    await conn.OpenAsync(cancellationToken);

    await using var cmd = new NpgsqlCommand(
      """
      INSERT INTO "PermissionAssignments"
        ("Id", "PermissionName", "PrincipalKind", "PrincipalId", "Effect",
         "ScopeKind", "ScopeId", "OwningTenantId", "IsEnabled", "CreatedByPrincipalType", "CreatedAt")
      VALUES
        (gen_random_uuid(), @permissionName, @principalKind,
         gen_random_uuid(), @effect, @scopeKind, @scopeId, @owningTenantId,
         true, 'system', CURRENT_TIMESTAMP)
      """,
      conn);
    cmd.Parameters.Add(new NpgsqlParameter("@permissionName", permissionName ?? PermissionNames.ServerTenantsWrite));
    cmd.Parameters.Add(new NpgsqlParameter("@principalKind", PermissionPrincipalKind.User.ToString()));
    cmd.Parameters.Add(new NpgsqlParameter("@effect", PermissionEffect.Allow.ToString()));
    cmd.Parameters.Add(new NpgsqlParameter("@scopeKind", scopeKind.ToString()));
    cmd.Parameters.Add(new NpgsqlParameter("@scopeId", (object?)scopeId ?? DBNull.Value));
    cmd.Parameters.Add(new NpgsqlParameter("@owningTenantId", (object?)owningTenantId ?? DBNull.Value));
    await cmd.ExecuteNonQueryAsync(cancellationToken);
  }

  private static async Task SeedAssignment(TestApp testApp, PermissionAssignment assignment)
  {
    using var scope = testApp.App.Services.CreateScope();
    await using var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    db.PermissionAssignments.Add(assignment);
    await db.SaveChangesAsync();
  }
}
