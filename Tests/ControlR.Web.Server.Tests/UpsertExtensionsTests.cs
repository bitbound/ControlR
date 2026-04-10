using ControlR.Web.Server.Data;
using ControlR.Web.Server.Data.Entities;
using ControlR.Web.Server.Data.Extensions;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ControlR.Web.Server.Tests;

public class UpsertExtensionsTests(ITestOutputHelper testOutput)
{
  private readonly ITestOutputHelper _testOutput = testOutput;

  [Fact]
  public async Task UpsertAsync_WhenConflictSelectorIsNotAProperty_ThrowsArgumentException()
  {
    var cancellationToken = TestContext.Current.CancellationToken;
    await using var testApp = await TestAppBuilder.CreateTestApp(
      _testOutput,
      testDatabaseName: $"{Guid.NewGuid()}");

    var tenant = await testApp.Services.CreateTestTenant();

    await using var scope = testApp.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDb>();

    var exception = await Assert.ThrowsAsync<ArgumentException>(() => db.UpsertAsync(
      new TenantSetting
      {
        Id = Guid.NewGuid(),
        Name = "instance-id",
        TenantId = tenant.Id,
        Value = "alpha"
      },
      [x => x.Name + x.Value],
      cancellationToken));

    Assert.Contains("must target a mapped property", exception.Message, StringComparison.OrdinalIgnoreCase);
  }

  [Fact]
  public async Task UpsertAsync_WhenConflictSelectorTargetsNavigationProperty_ThrowsInvalidOperationException()
  {
    var cancellationToken = TestContext.Current.CancellationToken;
    await using var testApp = await TestAppBuilder.CreateTestApp(
      _testOutput,
      testDatabaseName: $"{Guid.NewGuid()}");

    var tenant = await testApp.Services.CreateTestTenant();

    await using var scope = testApp.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDb>();

    var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => db.UpsertAsync(
      new TenantSetting
      {
        Id = Guid.NewGuid(),
        Name = "instance-id",
        TenantId = tenant.Id,
        Value = "alpha"
      },
      [x => x.Tenant!],
      cancellationToken));

    Assert.Contains("is not mapped", exception.Message, StringComparison.OrdinalIgnoreCase);
  }

  [Fact]
  public async Task UpsertAsync_WhenTrackedEntityAlreadyMatchesConflictKey_UpdatesTrackedEntityWithoutDuplicate()
  {
    var cancellationToken = TestContext.Current.CancellationToken;
    await using var testApp = await TestAppBuilder.CreateTestApp(
      _testOutput,
      testDatabaseName: $"{Guid.NewGuid()}");

    var tenant = await testApp.Services.CreateTestTenant();

    await using var scope = testApp.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDb>();

    var existingSetting = new TenantSetting
    {
      Id = Guid.NewGuid(),
      Name = "instance-id",
      TenantId = tenant.Id,
      Value = "alpha"
    };

    db.TenantSettings.Add(existingSetting);
    await db.SaveChangesAsync(cancellationToken);

    await db.UpsertAsync(
      new TenantSetting
      {
        Id = Guid.NewGuid(),
        Name = "instance-id",
        TenantId = tenant.Id,
        Value = "beta"
      },
      [x => x.Name, x => x.TenantId],
      cancellationToken);

    Assert.Equal("beta", existingSetting.Value);

    var storedSettings = await db.TenantSettings
      .AsNoTracking()
      .Where(x => x.TenantId == tenant.Id && x.Name == "instance-id")
      .ToListAsync(cancellationToken);

    var storedSetting = Assert.Single(storedSettings);
    Assert.Equal(existingSetting.Id, storedSetting.Id);
    Assert.Equal("beta", storedSetting.Value);
  }

  [Fact]
  public async Task UpsertAsync_WhenUsingConflictPropertiesWithInMemoryProvider_PreservesOriginalPrimaryKey()
  {
    var cancellationToken = TestContext.Current.CancellationToken;
    await using var testApp = await TestAppBuilder.CreateTestApp(
      _testOutput,
      testDatabaseName: $"{Guid.NewGuid()}");

    var tenant = await testApp.Services.CreateTestTenant();
    var originalId = Guid.NewGuid();

    await using var scope = testApp.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDb>();

    await db.UpsertAsync(
      new TenantSetting
      {
        Id = originalId,
        Name = "instance-id",
        TenantId = tenant.Id,
        Value = "alpha"
      },
      [x => x.Name, x => x.TenantId],
      cancellationToken);

    await db.UpsertAsync(
      new TenantSetting
      {
        Id = Guid.NewGuid(),
        Name = "instance-id",
        TenantId = tenant.Id,
        Value = "beta"
      },
      [x => x.Name, x => x.TenantId],
      cancellationToken);

    var storedSettings = await db.TenantSettings
      .AsNoTracking()
      .Where(x => x.TenantId == tenant.Id && x.Name == "instance-id")
      .ToListAsync(cancellationToken);

    var storedSetting = Assert.Single(storedSettings);
    Assert.Equal(originalId, storedSetting.Id);
    Assert.Equal("beta", storedSetting.Value);
  }

  [Fact]
  public async Task UpsertAsync_WhenUsingConflictPropertiesWithPostgres_IgnoresStoreGeneratedColumns()
  {
    var cancellationToken = TestContext.Current.CancellationToken;
    await using var testApp = await TestAppBuilder.CreateTestApp(
      _testOutput,
      testDatabaseName: $"{Guid.NewGuid()}",
      useInMemoryDatabase: false);

    var tenant = await testApp.Services.CreateTestTenant();
    var originalId = Guid.NewGuid();
    var requestedInsertCreatedAt = DateTimeOffset.Parse("2001-02-03T04:05:06+00:00");
    var requestedUpdateCreatedAt = DateTimeOffset.Parse("2002-03-04T05:06:07+00:00");

    await using (var arrangeScope = testApp.Services.CreateAsyncScope())
    {
      var db = arrangeScope.ServiceProvider.GetRequiredService<AppDb>();

      await db.UpsertAsync(
        new TenantSetting
        {
          Id = originalId,
          Name = "instance-id",
          TenantId = tenant.Id,
          Value = "alpha",
          CreatedAt = requestedInsertCreatedAt
        },
        [x => x.Name, x => x.TenantId],
        cancellationToken);
    }

    DateTimeOffset storedCreatedAt;
    await using (var firstAssertScope = testApp.Services.CreateAsyncScope())
    {
      var db = firstAssertScope.ServiceProvider.GetRequiredService<AppDb>();
      var storedSetting = await db.TenantSettings
        .AsNoTracking()
        .SingleAsync(x => x.TenantId == tenant.Id && x.Name == "instance-id", cancellationToken);

      Assert.Equal(originalId, storedSetting.Id);
      Assert.NotEqual(requestedInsertCreatedAt, storedSetting.CreatedAt);
      storedCreatedAt = storedSetting.CreatedAt;
    }

    await using (var updateScope = testApp.Services.CreateAsyncScope())
    {
      var db = updateScope.ServiceProvider.GetRequiredService<AppDb>();

      await db.UpsertAsync(
        new TenantSetting
        {
          Id = Guid.NewGuid(),
          Name = "instance-id",
          TenantId = tenant.Id,
          Value = "beta",
          CreatedAt = requestedUpdateCreatedAt
        },
        [x => x.Name, x => x.TenantId],
        cancellationToken);
    }

    await using var finalAssertScope = testApp.Services.CreateAsyncScope();
    var assertDb = finalAssertScope.ServiceProvider.GetRequiredService<AppDb>();
    var updatedSetting = await assertDb.TenantSettings
      .AsNoTracking()
      .SingleAsync(x => x.TenantId == tenant.Id && x.Name == "instance-id", cancellationToken);

    Assert.Equal(originalId, updatedSetting.Id);
    Assert.Equal("beta", updatedSetting.Value);
    Assert.Equal(storedCreatedAt, updatedSetting.CreatedAt);
    Assert.NotEqual(requestedUpdateCreatedAt, updatedSetting.CreatedAt);
  }

  [Fact]
  public async Task UpsertAsync_WhenUsingConflictProperties_UsesAlternateUniqueIndex()
  {
    var cancellationToken = TestContext.Current.CancellationToken;
    await using var testApp = await TestAppBuilder.CreateTestApp(
      _testOutput,
      testDatabaseName: $"{Guid.NewGuid()}",
      useInMemoryDatabase: false);

    var tenant = await testApp.Services.CreateTestTenant();
    var originalId = Guid.NewGuid();

    await using (var arrangeScope = testApp.Services.CreateAsyncScope())
    {
      var db = arrangeScope.ServiceProvider.GetRequiredService<AppDb>();

      await db.UpsertAsync(
        new TenantSetting
        {
          Id = originalId,
          Name = "instance-id",
          TenantId = tenant.Id,
          Value = "alpha"
        },
        cancellationToken);

      await db.UpsertAsync(
        new TenantSetting
        {
          Id = Guid.NewGuid(),
          Name = "instance-id",
          TenantId = tenant.Id,
          Value = "beta"
        },
        [x => x.Name, x => x.TenantId],
        cancellationToken);
    }

    await using var assertScope = testApp.Services.CreateAsyncScope();
    var assertDb = assertScope.ServiceProvider.GetRequiredService<AppDb>();
    var storedSettings = await assertDb.TenantSettings
      .AsNoTracking()
      .Where(x => x.TenantId == tenant.Id && x.Name == "instance-id")
      .ToListAsync(cancellationToken);

    var storedSetting = Assert.Single(storedSettings);
    Assert.Equal(originalId, storedSetting.Id);
    Assert.Equal("beta", storedSetting.Value);
  }

  [Fact]
  public async Task UpsertAsync_WhenUsingInMemoryProvider_FallsBackToEfCoreUpsert()
  {
    var cancellationToken = TestContext.Current.CancellationToken;
    await using var testApp = await TestAppBuilder.CreateTestApp(
      _testOutput,
      testDatabaseName: $"{Guid.NewGuid()}");

    var tenant = await testApp.Services.CreateTestTenant();

    await using var scope = testApp.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDb>();

    await db.UpsertAsync(
      new TenantSetting
      {
        Id = Guid.NewGuid(),
        Name = "instance-id",
        TenantId = tenant.Id,
        Value = "alpha"
      },
      [x => x.Name, x => x.TenantId],
      cancellationToken);

    await db.UpsertAsync(
      new TenantSetting
      {
        Id = Guid.NewGuid(),
        Name = "instance-id",
        TenantId = tenant.Id,
        Value = "beta"
      },
      [x => x.Name, x => x.TenantId],
      cancellationToken);

    var storedSettings = await db.TenantSettings
      .AsNoTracking()
      .Where(x => x.TenantId == tenant.Id && x.Name == "instance-id")
      .ToListAsync(cancellationToken);

    var storedSetting = Assert.Single(storedSettings);
    Assert.Equal("beta", storedSetting.Value);
  }

  [Fact]
  public async Task UpsertAsync_WhenUsingPrimaryKeyWithInMemoryProvider_UpdatesExistingRow()
  {
    var cancellationToken = TestContext.Current.CancellationToken;
    await using var testApp = await TestAppBuilder.CreateTestApp(
      _testOutput,
      testDatabaseName: $"{Guid.NewGuid()}");

    var tenant = await testApp.Services.CreateTestTenant();
    var settingId = Guid.NewGuid();

    await using var scope = testApp.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDb>();

    await db.UpsertAsync(
      new TenantSetting
      {
        Id = settingId,
        Name = "append-instance-id",
        TenantId = tenant.Id,
        Value = bool.TrueString
      },
      cancellationToken);

    await db.UpsertAsync(
      new TenantSetting
      {
        Id = settingId,
        Name = "append-instance-id",
        TenantId = tenant.Id,
        Value = bool.FalseString
      },
      cancellationToken);

    var storedSetting = await db.TenantSettings
      .AsNoTracking()
      .SingleAsync(x => x.Id == settingId, cancellationToken);

    Assert.Equal(bool.FalseString, storedSetting.Value);
  }

  [Fact]
  public async Task UpsertAsync_WhenUsingPrimaryKey_UpdatesExistingRow()
  {
    var cancellationToken = TestContext.Current.CancellationToken;
    await using var testApp = await TestAppBuilder.CreateTestApp(
      _testOutput,
      testDatabaseName: $"{Guid.NewGuid()}",
      useInMemoryDatabase: false);

    var tenant = await testApp.Services.CreateTestTenant();
    var settingId = Guid.NewGuid();

    await using (var arrangeScope = testApp.Services.CreateAsyncScope())
    {
      var db = arrangeScope.ServiceProvider.GetRequiredService<AppDb>();

      await db.UpsertAsync(
        new TenantSetting
        {
          Id = settingId,
          Name = "append-instance-id",
          TenantId = tenant.Id,
          Value = bool.TrueString
        },
        cancellationToken);

      await db.UpsertAsync(
        new TenantSetting
        {
          Id = settingId,
          Name = "append-instance-id",
          TenantId = tenant.Id,
          Value = bool.FalseString
        },
        cancellationToken);
    }

    await using var assertScope = testApp.Services.CreateAsyncScope();
    var assertDb = assertScope.ServiceProvider.GetRequiredService<AppDb>();
    var storedSetting = await assertDb.TenantSettings
      .AsNoTracking()
      .SingleAsync(x => x.Id == settingId, cancellationToken);

    Assert.Equal(bool.FalseString, storedSetting.Value);
    Assert.NotEqual(default, storedSetting.CreatedAt);
  }
}