using ControlR.Web.Server.Data;
using ControlR.Web.Server.Data.Entities;
using ControlR.Web.Server.Data.Enums;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ControlR.Web.Server.Tests.V0;

public class ServiceAccountInvariantInterceptorTests(ITestOutputHelper testOutput)
{
  [Fact]
  public async Task SaveChangesAllServerAccounts_Succeeds()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(testOutput);

    using var scope = testApp.CreateScope();
    var appDb = scope.ServiceProvider.GetRequiredService<AppDb>();

    var serverAccount = new ServiceAccount
    {
      Kind = ServiceAccountKind.Server,
      TenantId = null,
      Name = "Server SA",
      IsEnabled = true,
    };
    appDb.ServiceAccounts.Add(serverAccount);
    await appDb.SaveChangesAsync(TestContext.Current.CancellationToken);

    var fetched = await appDb.ServiceAccounts
      .IgnoreQueryFilters()
      .FirstOrDefaultAsync(x => x.Id == serverAccount.Id, TestContext.Current.CancellationToken);

    Assert.NotNull(fetched);
    Assert.Equal(ServiceAccountKind.Server, fetched.Kind);
    Assert.Null(fetched.TenantId);
  }

  [Fact]
  public async Task SaveChangesValidTenantAccount_Succeeds()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(testOutput);

    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;

    var tenant = await services.CreateTestTenant("Test Tenant");

    var tenantAccount = new ServiceAccount
    {
      Kind = ServiceAccountKind.Tenant,
      TenantId = tenant.Id,
      Name = "Tenant SA",
      IsEnabled = true,
    };

    var appDb = services.GetRequiredService<AppDb>();
    appDb.ServiceAccounts.Add(tenantAccount);
    await appDb.SaveChangesAsync(TestContext.Current.CancellationToken);

    var fetched = await appDb.ServiceAccounts
      .IgnoreQueryFilters()
      .FirstOrDefaultAsync(x => x.Id == tenantAccount.Id, TestContext.Current.CancellationToken);

    Assert.NotNull(fetched);
    Assert.Equal(ServiceAccountKind.Tenant, fetched.Kind);
    Assert.Equal(tenant.Id, fetched.TenantId);
  }

  [Fact]
  public async Task SaveChanges_DuplicateNameExistingInDatabase_Sync_Throws()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(testOutput);

    using var scope = testApp.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDb>();

    db.ServiceAccounts.Add(new ServiceAccount
    {
      Kind = ServiceAccountKind.Server,
      TenantId = null,
      Name = "existing-sync",
      IsEnabled = true,
    });
    db.SaveChanges();

    using var scope2 = testApp.CreateScope();
    var db2 = scope2.ServiceProvider.GetRequiredService<AppDb>();
    db2.ServiceAccounts.Add(new ServiceAccount
    {
      Kind = ServiceAccountKind.Server,
      TenantId = null,
      Name = "existing-sync",
      IsEnabled = true,
    });

    var ex = Assert.ThrowsAny<InvalidOperationException>(() => db2.SaveChanges());
    Assert.Contains("already exists", ex.Message);
  }

  [Fact]
  public async Task SaveChanges_DuplicateNameExistingInDatabase_Throws()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(testOutput);

    using var scope = testApp.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDb>();

    var existing = new ServiceAccount
    {
      Kind = ServiceAccountKind.Server,
      TenantId = null,
      Name = "existing",
      IsEnabled = true,
    };
    db.ServiceAccounts.Add(existing);
    await db.SaveChangesAsync(TestContext.Current.CancellationToken);

    // New DbContext to avoid change-tracker interference.
    using var scope2 = testApp.CreateScope();
    var db2 = scope2.ServiceProvider.GetRequiredService<AppDb>();
    db2.ServiceAccounts.Add(new ServiceAccount
    {
      Kind = ServiceAccountKind.Server,
      TenantId = null,
      Name = "existing",
      IsEnabled = true,
    });

    var ex = await Assert.ThrowsAnyAsync<InvalidOperationException>(
      () => db2.SaveChangesAsync(TestContext.Current.CancellationToken));
    Assert.Contains("already exists", ex.Message);
  }

  [Fact]
  public async Task SaveChanges_DuplicateServerNameSameBatch_Throws()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(testOutput);

    using var scope = testApp.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDb>();

    db.ServiceAccounts.Add(new ServiceAccount
    {
      Kind = ServiceAccountKind.Server,
      TenantId = null,
      Name = "dup",
      IsEnabled = true,
    });
    db.ServiceAccounts.Add(new ServiceAccount
    {
      Kind = ServiceAccountKind.Server,
      TenantId = null,
      Name = "dup",
      IsEnabled = true,
    });

    await Assert.ThrowsAnyAsync<InvalidOperationException>(
      () => db.SaveChangesAsync(TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task SaveChanges_DuplicateTenantNameSameBatch_Throws()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(testOutput);

    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var tenant = await services.CreateTestTenant("T");

    var db = services.GetRequiredService<AppDb>();
    db.ServiceAccounts.Add(new ServiceAccount
    {
      Kind = ServiceAccountKind.Tenant,
      TenantId = tenant.Id,
      Name = "dup",
      IsEnabled = true,
    });
    db.ServiceAccounts.Add(new ServiceAccount
    {
      Kind = ServiceAccountKind.Tenant,
      TenantId = tenant.Id,
      Name = "dup",
      IsEnabled = true,
    });

    await Assert.ThrowsAnyAsync<InvalidOperationException>(
      () => db.SaveChangesAsync(TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task SaveChanges_SameNameDifferentTenants_Succeeds()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(testOutput);

    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var tenantA = await services.CreateTestTenant("A");
    var tenantB = await services.CreateTestTenant("B");

    var db = services.GetRequiredService<AppDb>();
    db.ServiceAccounts.Add(new ServiceAccount
    {
      Kind = ServiceAccountKind.Tenant,
      TenantId = tenantA.Id,
      Name = "shared-name",
      IsEnabled = true,
    });
    db.ServiceAccounts.Add(new ServiceAccount
    {
      Kind = ServiceAccountKind.Tenant,
      TenantId = tenantB.Id,
      Name = "shared-name",
      IsEnabled = true,
    });

    await db.SaveChangesAsync(TestContext.Current.CancellationToken);
  }

  [Fact]
  public async Task SaveChanges_ServerAccountWithTenantId_Throws()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(testOutput);

    using var scope = testApp.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDb>();

    var badAccount = new ServiceAccount
    {
      Kind = ServiceAccountKind.Server,
      TenantId = Guid.NewGuid(),
      Name = "Bad Server SA",
      IsEnabled = true,
    };
    db.ServiceAccounts.Add(badAccount);

    var ex = await Assert.ThrowsAnyAsync<Exception>(
      () => db.SaveChangesAsync(TestContext.Current.CancellationToken));
    Assert.IsType<InvalidOperationException>(ex.InnerException ?? ex);
    Assert.Contains("Server-scoped service accounts must have a null TenantId", ex.Message);
  }

  [Fact]
  public async Task SaveChanges_TenantAccountSameNameDifferentTenantExisting_Succeeds()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(testOutput);

    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var tenantA = await services.CreateTestTenant("A");
    var tenantB = await services.CreateTestTenant("B");

    var db = services.GetRequiredService<AppDb>();
    db.ServiceAccounts.Add(new ServiceAccount
    {
      Kind = ServiceAccountKind.Tenant,
      TenantId = tenantA.Id,
      Name = "cross-tenant-name",
      IsEnabled = true,
    });
    await db.SaveChangesAsync(TestContext.Current.CancellationToken);

    using var scope2 = testApp.CreateScope();
    var db2 = scope2.ServiceProvider.GetRequiredService<AppDb>();
    db2.ServiceAccounts.Add(new ServiceAccount
    {
      Kind = ServiceAccountKind.Tenant,
      TenantId = tenantB.Id,
      Name = "cross-tenant-name",
      IsEnabled = true,
    });

    await db2.SaveChangesAsync(TestContext.Current.CancellationToken);
  }

  [Fact]
  public async Task SaveChanges_TenantAccountWithNullTenantId_Throws()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(testOutput);

    using var scope = testApp.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDb>();

    var badAccount = new ServiceAccount
    {
      Kind = ServiceAccountKind.Tenant,
      TenantId = null,
      Name = "Bad Tenant SA",
      IsEnabled = true,
    };
    db.ServiceAccounts.Add(badAccount);

    var ex = await Assert.ThrowsAnyAsync<Exception>(
      () => db.SaveChangesAsync(TestContext.Current.CancellationToken));
    Assert.IsType<InvalidOperationException>(ex.InnerException ?? ex);
    Assert.Contains("Tenant-scoped service accounts must have a non-null TenantId", ex.Message);
  }

  [Fact]
  public async Task UpdateServerAccountToHaveTenantId_Throws()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(testOutput);

    using var scope = testApp.CreateScope();
    var appDb = scope.ServiceProvider.GetRequiredService<AppDb>();

    var account = new ServiceAccount
    {
      Kind = ServiceAccountKind.Server,
      TenantId = null,
      Name = "Initially Valid",
      IsEnabled = true,
    };
    appDb.ServiceAccounts.Add(account);
    await appDb.SaveChangesAsync(TestContext.Current.CancellationToken);

    account.TenantId = Guid.NewGuid();

    var ex = await Assert.ThrowsAnyAsync<Exception>(
      () => appDb.SaveChangesAsync(TestContext.Current.CancellationToken));
    Assert.IsType<InvalidOperationException>(ex.InnerException ?? ex);
    Assert.Contains("Server-scoped service accounts must have a null TenantId", ex.Message);
  }

  [Fact]
  public async Task UpdateTenantAccountToRemoveTenantId_Throws()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(testOutput);

    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;

    var tenant = await services.CreateTestTenant("Test Tenant 2");

    var db = services.GetRequiredService<AppDb>();

    var account = new ServiceAccount
    {
      Kind = ServiceAccountKind.Tenant,
      TenantId = tenant.Id,
      Name = "Initially Valid",
      IsEnabled = true,
    };
    db.ServiceAccounts.Add(account);
    await db.SaveChangesAsync(TestContext.Current.CancellationToken);

    account.TenantId = null;

    var ex = await Assert.ThrowsAnyAsync<Exception>(
      () => db.SaveChangesAsync(TestContext.Current.CancellationToken));
    Assert.IsType<InvalidOperationException>(ex.InnerException ?? ex);
    Assert.Contains("Tenant-scoped service accounts must have a non-null TenantId", ex.Message);
  }
}
