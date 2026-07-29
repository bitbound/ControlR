using ControlR.Web.Server.Data;
using ControlR.Web.Server.Data.Entities;
using ControlR.Web.Server.Data.Extensions;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ControlR.Web.Server.Tests;

public class AppDbExtensionsTests(ITestOutputHelper testOutput)
{
  private readonly ITestOutputHelper _testOutput = testOutput;

  [Fact]
  public async Task AddOrUpdate_WhenTrackedEntityMatches_UpdatesTrackedEntityWithoutDuplicate()
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
      Name = "instance-id",
      TenantId = tenant.Id,
      Value = "alpha"
    };

    db.TenantSettings.Add(existingSetting);
    await db.SaveChangesAsync(cancellationToken);

    await db.AddOrUpdate(
      new TenantSetting
      {
        Name = "instance-id",
        TenantId = tenant.Id,
        Value = "beta"
      },
      x => x.Name == "instance-id" && x.TenantId == tenant.Id,
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
  public async Task AddOrUpdate_WhenUpdating_IgnoresStoreGeneratedColumns()
  {
    var cancellationToken = TestContext.Current.CancellationToken;
    await using var testApp = await TestAppBuilder.CreateTestApp(
      _testOutput,
      testDatabaseName: $"{Guid.NewGuid()}",
      useInMemoryDatabase: false);

    var tenant = await testApp.Services.CreateTestTenant();
    var requestedUpdateCreatedAt = DateTimeOffset.Parse("2002-03-04T05:06:07+00:00");

    await using (var arrangeScope = testApp.Services.CreateAsyncScope())
    {
      var db = arrangeScope.ServiceProvider.GetRequiredService<AppDb>();

      var entity = new TenantSetting
      {
        Name = "instance-id",
        TenantId = tenant.Id,
        Value = "alpha"
      };

      db.TenantSettings.Add(entity);
      await db.SaveChangesAsync(cancellationToken);
    }

    Guid originalId;
    DateTimeOffset storedCreatedAt;
    await using (var firstAssertScope = testApp.Services.CreateAsyncScope())
    {
      var db = firstAssertScope.ServiceProvider.GetRequiredService<AppDb>();
      var storedSetting = await db.TenantSettings
        .AsNoTracking()
        .SingleAsync(x => x.TenantId == tenant.Id && x.Name == "instance-id", cancellationToken);

      Assert.NotEqual(Guid.Empty, storedSetting.Id);
      originalId = storedSetting.Id;
      storedCreatedAt = storedSetting.CreatedAt;
    }

    await using (var updateScope = testApp.Services.CreateAsyncScope())
    {
      var db = updateScope.ServiceProvider.GetRequiredService<AppDb>();

      await db.AddOrUpdate(
        new TenantSetting
        {
          Name = "instance-id",
          TenantId = tenant.Id,
          Value = "beta",
          CreatedAt = requestedUpdateCreatedAt
        },
        x => x.Name == "instance-id" && x.TenantId == tenant.Id,
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
  public async Task AddOrUpdate_WhenUpdating_PreservesOriginalPrimaryKey()
  {
    var cancellationToken = TestContext.Current.CancellationToken;
    await using var testApp = await TestAppBuilder.CreateTestApp(
      _testOutput,
      testDatabaseName: $"{Guid.NewGuid()}");

    var tenant = await testApp.Services.CreateTestTenant();

    await using var scope = testApp.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDb>();

    var entity = new TenantSetting
    {
      Name = "instance-id",
      TenantId = tenant.Id,
      Value = "alpha"
    };

    db.TenantSettings.Add(entity);
    await db.SaveChangesAsync(cancellationToken);
    var originalId = entity.Id;

    await db.AddOrUpdate(
      new TenantSetting
      {
        Name = "instance-id",
        TenantId = tenant.Id,
        Value = "beta"
      },
      x => x.Name == "instance-id" && x.TenantId == tenant.Id,
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
  public async Task AddOrUpdate_WithInMemoryProvider_UpdatesExistingRow()
  {
    var cancellationToken = TestContext.Current.CancellationToken;
    await using var testApp = await TestAppBuilder.CreateTestApp(
      _testOutput,
      testDatabaseName: $"{Guid.NewGuid()}");

    var tenant = await testApp.Services.CreateTestTenant();

    await using var scope = testApp.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDb>();

    await db.AddOrUpdate(
      new TenantSetting
      {
        Name = "instance-id",
        TenantId = tenant.Id,
        Value = "alpha"
      },
      x => x.Name == "instance-id" && x.TenantId == tenant.Id,
      cancellationToken);

    await db.AddOrUpdate(
      new TenantSetting
      {
        Name = "instance-id",
        TenantId = tenant.Id,
        Value = "beta"
      },
      x => x.Name == "instance-id" && x.TenantId == tenant.Id,
      cancellationToken);

    var storedSettings = await db.TenantSettings
      .AsNoTracking()
      .Where(x => x.TenantId == tenant.Id && x.Name == "instance-id")
      .ToListAsync(cancellationToken);

    var storedSetting = Assert.Single(storedSettings);
    Assert.Equal("beta", storedSetting.Value);
  }

  [Fact]
  public async Task AddOrUpdate_WithNaturalKey_UsesAlternateUniqueIndex()
  {
    var cancellationToken = TestContext.Current.CancellationToken;
    await using var testApp = await TestAppBuilder.CreateTestApp(
      _testOutput,
      testDatabaseName: $"{Guid.NewGuid()}",
      useInMemoryDatabase: false);

    var tenant = await testApp.Services.CreateTestTenant();

    Guid originalId;
    await using (var arrangeScope = testApp.Services.CreateAsyncScope())
    {
      var db = arrangeScope.ServiceProvider.GetRequiredService<AppDb>();

      var entity = new TenantSetting
      {
        Name = "instance-id",
        TenantId = tenant.Id,
        Value = "alpha"
      };

      db.TenantSettings.Add(entity);
      await db.SaveChangesAsync(cancellationToken);
      originalId = entity.Id;
    }

    await using (var updateScope = testApp.Services.CreateAsyncScope())
    {
      var db = updateScope.ServiceProvider.GetRequiredService<AppDb>();

      await db.AddOrUpdate(
        new TenantSetting
        {
          Name = "instance-id",
          TenantId = tenant.Id,
          Value = "beta"
        },
        x => x.Name == "instance-id" && x.TenantId == tenant.Id,
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
  public async Task AddOrUpdate_WithPrimaryKey_UpdatesExistingRow()
  {
    var cancellationToken = TestContext.Current.CancellationToken;
    await using var testApp = await TestAppBuilder.CreateTestApp(
      _testOutput,
      testDatabaseName: $"{Guid.NewGuid()}",
      useInMemoryDatabase: false);

    var tenant = await testApp.Services.CreateTestTenant();

    Guid settingId;
    await using (var arrangeScope = testApp.Services.CreateAsyncScope())
    {
      var db = arrangeScope.ServiceProvider.GetRequiredService<AppDb>();

      var entity = new TenantSetting
      {
        Name = "append-instance-id",
        TenantId = tenant.Id,
        Value = bool.TrueString
      };

      db.TenantSettings.Add(entity);
      await db.SaveChangesAsync(cancellationToken);
      settingId = entity.Id;
    }

    await using (var updateScope = testApp.Services.CreateAsyncScope())
    {
      var db = updateScope.ServiceProvider.GetRequiredService<AppDb>();

      await db.AddOrUpdate(
        new TenantSetting
        {
          Name = "append-instance-id",
          TenantId = tenant.Id,
          Value = bool.FalseString
        },
        x => x.Id == settingId,
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
