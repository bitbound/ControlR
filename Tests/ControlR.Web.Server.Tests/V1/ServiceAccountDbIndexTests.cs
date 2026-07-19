using ControlR.Web.Server.Data;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ControlR.Web.Server.Tests.V1;

/// <summary>
/// Verifies database-level unique index behavior for ServiceAccounts.
/// These tests require a real PostgreSQL instance (testcontainers).
/// </summary>
public class ServiceAccountDbIndexTests(ITestOutputHelper testOutput)
{
  [Fact]
  public async Task RawSql_DuplicateServerName_ThrowsDbException()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(
      testOutput,
      useInMemoryDatabase: false);

    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    await using var appDb = services.GetRequiredService<AppDb>();

    var tenant = await services.CreateTestTenant("Server Test Tenant");

    // Open a raw connection (independent of EF Core's connection management).
    var connStr = appDb.Database.GetConnectionString()
      ?? throw new InvalidOperationException("No connection string");

    var conn = new NpgsqlConnection(connStr);
    await conn.OpenAsync(TestContext.Current.CancellationToken);
    await using var tx = await conn.BeginTransactionAsync(TestContext.Current.CancellationToken);

    try
    {
      // Insert first server-scoped account via raw SQL.
      await using var cmd1 = new NpgsqlCommand(
        """
        INSERT INTO "ServiceAccounts" ("Id", "Kind", "TenantId", "Name", "Description", "IsEnabled", "CreatedAt")
        VALUES (gen_random_uuid(), 'Server', NULL, 'ServerDupName', NULL, true, CURRENT_TIMESTAMP)
        """,
        conn, tx);
      await cmd1.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);

      // Insert duplicate via raw SQL.
      await using var cmd2 = new NpgsqlCommand(
        """
        INSERT INTO "ServiceAccounts" ("Id", "Kind", "TenantId", "Name", "Description", "IsEnabled", "CreatedAt")
        VALUES (gen_random_uuid(), 'Server', NULL, 'ServerDupName', NULL, true, CURRENT_TIMESTAMP)
        """,
        conn, tx);
      var ex = await Assert.ThrowsAsync<Npgsql.PostgresException>(
        () => cmd2.ExecuteNonQueryAsync(TestContext.Current.CancellationToken));

      Assert.Contains("IX_ServiceAccounts_Server_Name", ex.Message);
    }
    catch
    {
      await tx.RollbackAsync(TestContext.Current.CancellationToken);
      throw;
    }
  }

  [Fact]
  public async Task RawSql_DuplicateTenantScopedSameTenant_ThrowsDbException()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(
      testOutput,
      useInMemoryDatabase: false);

    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    await using var appDb = services.GetRequiredService<AppDb>();

    var tenant = await services.CreateTestTenant("Target Tenant");

    // Open the DB connection and start a transaction to isolate raw SQL.
    var connection = appDb.Database.GetDbConnection() as NpgsqlConnection
      ?? throw new InvalidOperationException("Expected NpgsqlConnection");

    await connection.OpenAsync(TestContext.Current.CancellationToken);
    await using var tx = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken);

    try
    {
      // Insert the first tenant-scoped account via raw SQL.
      await using var cmd1 = new NpgsqlCommand(
        """
        INSERT INTO "ServiceAccounts" ("Id", "Kind", "TenantId", "Name", "Description", "IsEnabled", "CreatedAt")
        VALUES (gen_random_uuid(), 'Tenant', @tenantId, 'UniqueName', NULL, true, CURRENT_TIMESTAMP)
        """,
        connection, tx);
      cmd1.Parameters.AddWithValue("tenantId", tenant.Id);
      await cmd1.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);

      // Insert the duplicate — same (Kind, TenantId, Name).
      await using var cmd2 = new NpgsqlCommand(
        """
        INSERT INTO "ServiceAccounts" ("Id", "Kind", "TenantId", "Name", "Description", "IsEnabled", "CreatedAt")
        VALUES (gen_random_uuid(), 'Tenant', @tenantId, 'UniqueName', NULL, true, CURRENT_TIMESTAMP)
        """,
        connection, tx);
      cmd2.Parameters.AddWithValue("tenantId", tenant.Id);

      var ex = await Assert.ThrowsAsync<Npgsql.PostgresException>(
        () => cmd2.ExecuteNonQueryAsync(TestContext.Current.CancellationToken));

      Assert.Contains("IX_ServiceAccounts_TenantId_Name", ex.Message);
    }
    catch
    {
      await tx.RollbackAsync(TestContext.Current.CancellationToken);
      throw;
    }
  }

  [Fact]
  public async Task RawSql_ServerAccountWithTenantId_ThrowsCheckConstraint()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(
      testOutput,
      useInMemoryDatabase: false);

    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    await using var appDb = services.GetRequiredService<AppDb>();
    var tenant = await services.CreateTestTenant("Constraint Tenant");

    var connStr = appDb.Database.GetConnectionString()
      ?? throw new InvalidOperationException("No connection string");

    await using var conn = new NpgsqlConnection(connStr);
    await conn.OpenAsync(TestContext.Current.CancellationToken);

    await using var cmd = new NpgsqlCommand(
      """
      INSERT INTO "ServiceAccounts" ("Id", "Kind", "TenantId", "Name", "Description", "IsEnabled", "CreatedAt")
      VALUES (gen_random_uuid(), 'Server', @tenantId, 'InvalidServerScope', NULL, true, CURRENT_TIMESTAMP)
      """,
      conn);
    cmd.Parameters.AddWithValue("tenantId", tenant.Id);

    var ex = await Assert.ThrowsAsync<Npgsql.PostgresException>(
      () => cmd.ExecuteNonQueryAsync(TestContext.Current.CancellationToken));

    Assert.Contains("CK_ServiceAccounts_Kind_TenantId", ex.Message);
  }
}
