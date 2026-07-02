using ControlR.Web.Server.Data;
using ControlR.Web.Server.Data.Entities;
using ControlR.Web.Server.Data.Enums;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace ControlR.Web.Server.Tests;

/// <summary>
/// Verifies database-level unique index behavior for ServiceAccounts.
/// These tests require a real PostgreSQL instance (testcontainers).
/// </summary>
public class ServiceAccountDbIndexTests(ITestOutputHelper testOutput)
{
  [Fact]
  public async Task RawSql_DuplicateTenantScopedSameTenant_ThrowsDbException()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(
      testOutput,
      useInMemoryDatabase: false);

    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var appDb = services.GetRequiredService<AppDb>();

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

      Assert.Contains("IX_ServiceAccounts_Kind_TenantId_Name", ex.Message);
    }
    catch
    {
      await tx.RollbackAsync(TestContext.Current.CancellationToken);
      throw;
    }
  }

  [Fact]
  public async Task RawSql_InsertDuplicateServerName_Succeeds_BecausePartialIndexExcludesNulls()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(
      testOutput,
      useInMemoryDatabase: false);

    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var appDb = services.GetRequiredService<AppDb>();

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
      await cmd2.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);

      await tx.CommitAsync(TestContext.Current.CancellationToken);
    }
    catch
    {
      await tx.RollbackAsync(TestContext.Current.CancellationToken);
      throw;
    }

    // Verify both were inserted.
    var count = await appDb.ServiceAccounts
      .IgnoreQueryFilters()
      .CountAsync(x => x.Name == "ServerDupName" && x.Kind == ServiceAccountKind.Server,
        TestContext.Current.CancellationToken);

    Assert.Equal(2, count);
  }

  [Fact]
  public async Task SaveChanges_SameNameDifferentTenants_Succeeds()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(
      testOutput,
      useInMemoryDatabase: false);

    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var tenantA = await services.CreateTestTenant("Tenant A");
    var tenantB = await services.CreateTestTenant("Tenant B");

    var appDb = services.GetRequiredService<AppDb>();

    appDb.ServiceAccounts.Add(new ServiceAccount
    {
      Kind = ServiceAccountKind.Tenant,
      TenantId = tenantA.Id,
      Name = "SharedName",
      IsEnabled = true,
    });
    await appDb.SaveChangesAsync(TestContext.Current.CancellationToken);

    using var scope2 = testApp.CreateScope();
    var appDb2 = scope2.ServiceProvider.GetRequiredService<AppDb>();

    appDb2.ServiceAccounts.Add(new ServiceAccount
    {
      Kind = ServiceAccountKind.Tenant,
      TenantId = tenantB.Id,
      Name = "SharedName",
      IsEnabled = true,
    });

    await appDb2.SaveChangesAsync(TestContext.Current.CancellationToken);
  }
}
