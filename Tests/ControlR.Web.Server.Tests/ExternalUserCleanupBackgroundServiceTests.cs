using ControlR.Web.Server.Data;
using ControlR.Web.Server.Data.Entities;
using ControlR.Web.Server.Data.Enums;
using ControlR.Web.Server.Services.ExternalUsers;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ControlR.Web.Server.Tests;

public class ExternalUserCleanupBackgroundServiceTests(ITestOutputHelper testOutput)
{
  private readonly ITestOutputHelper _testOutput = testOutput;

  [Fact]
  public async Task CleanExpiredExternalUsers_NeverLoggedIn_AreNotRemoved()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput,
      testDatabaseName: $"{Guid.NewGuid()}",
      extraConfiguration: new Dictionary<string, string?>
      {
        { "AppOptions:ExternalUserCleanupAfterDays", "1" }
      });

    var backgroundService = testApp.Services.GetRequiredService<ExternalUserCleanupBackgroundService>();
    var tenant = await testApp.Services.CreateTestTenant();

    using var setupScope = testApp.CreateScope();
    var userManager = setupScope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

    var neverLoggedIn = new AppUser
    {
      UserName = $"ext-never-{Guid.NewGuid()}",
      Email = $"ext-never-{Guid.NewGuid()}@controlr.local",
      TenantId = tenant.Id,
      AccountType = AccountType.ExternalUser,
      LastLogin = null
    };
    await userManager.CreateAsync(neverLoggedIn);

    testApp.TimeProvider.Advance(TimeSpan.FromDays(30));

    var removedCount = await backgroundService.CleanExpiredExternalUsers(TestContext.Current.CancellationToken);

    Assert.Equal(0, removedCount);

    using var verifyScope = testApp.CreateScope();
    var db = verifyScope.ServiceProvider.GetRequiredService<AppDb>();
    var exists = await db.Users.AnyAsync(x => x.Id == neverLoggedIn.Id, TestContext.Current.CancellationToken);
    Assert.True(exists);
  }

  [Fact]
  public async Task CleanExpiredExternalUsers_OnlyRemovesExternalAccounts()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput,
      testDatabaseName: $"{Guid.NewGuid()}",
      extraConfiguration: new Dictionary<string, string?>
      {
        { "AppOptions:ExternalUserCleanupAfterDays", "1" }
      });

    var backgroundService = testApp.Services.GetRequiredService<ExternalUserCleanupBackgroundService>();
    var tenant = await testApp.Services.CreateTestTenant();

    using var setupScope = testApp.CreateScope();
    var userManager = setupScope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

    var staleExternal = new AppUser
    {
      UserName = $"ext-{Guid.NewGuid()}",
      Email = $"ext-{Guid.NewGuid()}@controlr.local",
      TenantId = tenant.Id,
      AccountType = AccountType.ExternalUser,
      LastLogin = DateTimeOffset.UtcNow
    };
    await userManager.CreateAsync(staleExternal);

    var regularUser = await testApp.Services.CreateTestUser(tenant.Id);

    testApp.TimeProvider.Advance(TimeSpan.FromDays(2));

    var removedCount = await backgroundService.CleanExpiredExternalUsers(TestContext.Current.CancellationToken);

    Assert.Equal(1, removedCount);

    using var verifyScope = testApp.CreateScope();
    var db = verifyScope.ServiceProvider.GetRequiredService<AppDb>();
    var regularExists = await db.Users.AnyAsync(x => x.Id == regularUser.Id, TestContext.Current.CancellationToken);
    Assert.True(regularExists);
  }

  [Fact]
  public async Task CleanExpiredExternalUsers_RemovesUsersWithExpiredLastLogin()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput,
      testDatabaseName: $"{Guid.NewGuid()}",
      extraConfiguration: new Dictionary<string, string?>
      {
        { "AppOptions:ExternalUserCleanupAfterDays", "1" }
      });

    var backgroundService = testApp.Services.GetRequiredService<ExternalUserCleanupBackgroundService>();
    var tenant = await testApp.Services.CreateTestTenant();

    using var setupScope = testApp.CreateScope();
    var userManager = setupScope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

    var staleUser = new AppUser
    {
      UserName = $"ext-stale-{Guid.NewGuid()}",
      Email = $"ext-stale-{Guid.NewGuid()}@controlr.local",
      TenantId = tenant.Id,
      AccountType = AccountType.ExternalUser,
      LastLogin = DateTimeOffset.UtcNow
    };
    await userManager.CreateAsync(staleUser);

    testApp.TimeProvider.Advance(TimeSpan.FromDays(2));

    var freshUser = new AppUser
    {
      UserName = $"ext-fresh-{Guid.NewGuid()}",
      Email = $"ext-fresh-{Guid.NewGuid()}@controlr.local",
      TenantId = tenant.Id,
      AccountType = AccountType.ExternalUser,
      LastLogin = testApp.TimeProvider.GetUtcNow()
    };
    await userManager.CreateAsync(freshUser);

    var removedCount = await backgroundService.CleanExpiredExternalUsers(TestContext.Current.CancellationToken);

    Assert.Equal(1, removedCount);

    using var verifyScope = testApp.CreateScope();
    var db = verifyScope.ServiceProvider.GetRequiredService<AppDb>();
    var staleExists = await db.Users.AnyAsync(x => x.Id == staleUser.Id, TestContext.Current.CancellationToken);
    var freshExists = await db.Users.AnyAsync(x => x.Id == freshUser.Id, TestContext.Current.CancellationToken);

    Assert.False(staleExists);
    Assert.True(freshExists);
  }

  [Fact]
  public async Task CleanExpiredExternalUsers_WhenCleanupDisabled_DoesNothing()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput,
      testDatabaseName: $"{Guid.NewGuid()}",
      extraConfiguration: new Dictionary<string, string?>
      {
        { "AppOptions:ExternalUserCleanupAfterDays", "0" }
      });

    var backgroundService = testApp.Services.GetRequiredService<ExternalUserCleanupBackgroundService>();
    var tenant = await testApp.Services.CreateTestTenant();

    using var setupScope = testApp.CreateScope();
    var userManager = setupScope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

    var externalUser = new AppUser
    {
      UserName = $"ext-{Guid.NewGuid()}",
      Email = $"ext-{Guid.NewGuid()}@controlr.local",
      TenantId = tenant.Id,
      AccountType = AccountType.ExternalUser,
      LastLogin = DateTimeOffset.UtcNow
    };
    await userManager.CreateAsync(externalUser);

    testApp.TimeProvider.Advance(TimeSpan.FromDays(30));

    var removedCount = await backgroundService.CleanExpiredExternalUsers(TestContext.Current.CancellationToken);

    Assert.Equal(0, removedCount);
  }
}
