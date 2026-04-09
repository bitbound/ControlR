using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi;
using ControlR.Web.Server.Data;
using ControlR.Web.Server.Data.Entities;
using ControlR.Web.Server.Services.Settings;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace ControlR.Web.Server.Tests;

public class EffectiveUserPreferencesResolverTests(ITestOutputHelper testOutput) : IAsyncLifetime
{
  private readonly ITestOutputHelper _testOutput = testOutput;

  private TestApp _testApp = null!;

  public async ValueTask DisposeAsync()
  {
    await _testApp.DisposeAsync();
  }

  [Fact]
  public async Task GetEffectiveUserPreferences_WhenTenantSettingExists_ReturnsTenantEnforcedDto()
  {
    var tenant = await _testApp.Services.CreateTestTenant();
    var user = await _testApp.Services.CreateTestUser(tenant.Id, email: $"user-{Guid.NewGuid():N}@test.local");

    await SeedTenantSetting(tenant.Id, bool.FalseString);
    await SeedUserPreference(user.Id, bool.TrueString);

    var result = await ResolveEffectivePreferences(tenant.Id, user.Id);

    Assert.False(result.NotifyUserOnSessionStart);
    Assert.True(result.IsNotifyUserOnSessionStartTenantEnforced);
  }

  [Fact]
  public async Task GetEffectiveUserPreferences_WhenTenantSettingIsInvalid_UsesUserPreferenceWithoutEnforcement()
  {
    var tenant = await _testApp.Services.CreateTestTenant();
    var user = await _testApp.Services.CreateTestUser(tenant.Id, email: $"user-{Guid.NewGuid():N}@test.local");

    await SeedTenantSetting(tenant.Id, "invalid-bool");
    await SeedUserPreference(user.Id, bool.FalseString);

    var result = await ResolveEffectivePreferences(tenant.Id, user.Id);

    Assert.False(result.NotifyUserOnSessionStart);
    Assert.False(result.IsNotifyUserOnSessionStartTenantEnforced);
  }

  [Fact]
  public async Task GetNotifyUserOnSessionStart_WhenNoStoredValuesExist_ReturnsDefaultValue()
  {
    var tenant = await _testApp.Services.CreateTestTenant();
    var user = await _testApp.Services.CreateTestUser(tenant.Id, email: $"user-{Guid.NewGuid():N}@test.local");

    var result = await ResolveNotifyUser(tenant.Id, user.Id);

    Assert.True(result);
  }

  [Fact]
  public async Task GetNotifyUserOnSessionStart_WhenTenantSettingExists_ReturnsTenantValue()
  {
    var tenant = await _testApp.Services.CreateTestTenant();
    var user = await _testApp.Services.CreateTestUser(tenant.Id, email: $"user-{Guid.NewGuid():N}@test.local");

    await SeedTenantSetting(tenant.Id, bool.FalseString);
    await SeedUserPreference(user.Id, bool.TrueString);

    var result = await ResolveNotifyUser(tenant.Id, user.Id);

    Assert.False(result);
  }

  [Fact]
  public async Task GetNotifyUserOnSessionStart_WhenTenantSettingIsInvalid_FallsBackToUserPreference()
  {
    var tenant = await _testApp.Services.CreateTestTenant();
    var user = await _testApp.Services.CreateTestUser(tenant.Id, email: $"user-{Guid.NewGuid():N}@test.local");

    await SeedTenantSetting(tenant.Id, "invalid-bool");
    await SeedUserPreference(user.Id, bool.FalseString);

    var result = await ResolveNotifyUser(tenant.Id, user.Id);

    Assert.False(result);
  }

  [Fact]
  public async Task GetNotifyUserOnSessionStart_WhenTenantSettingMissing_UsesUserPreference()
  {
    var tenant = await _testApp.Services.CreateTestTenant();
    var user = await _testApp.Services.CreateTestUser(tenant.Id, email: $"user-{Guid.NewGuid():N}@test.local");

    await SeedUserPreference(user.Id, bool.FalseString);

    var result = await ResolveNotifyUser(tenant.Id, user.Id);

    Assert.False(result);
  }

  [Fact]
  public async Task GetNotifyUserOnSessionStart_WhenUserPreferenceIsInvalid_ReturnsDefaultValue()
  {
    var tenant = await _testApp.Services.CreateTestTenant();
    var user = await _testApp.Services.CreateTestUser(tenant.Id, email: $"user-{Guid.NewGuid():N}@test.local");

    await SeedUserPreference(user.Id, "invalid-bool");

    var result = await ResolveNotifyUser(tenant.Id, user.Id);

    Assert.True(result);
  }

  public async ValueTask InitializeAsync()
  {
    _testApp = await TestAppBuilder.CreateTestApp(_testOutput, testDatabaseName: $"{Guid.NewGuid()}");
  }

  private async Task<EffectiveUserPreferencesDto> ResolveEffectivePreferences(Guid tenantId, Guid userId)
  {
    await using var scope = _testApp.Services.CreateAsyncScope();
    var resolver = scope.ServiceProvider.GetRequiredService<IEffectiveUserPreferencesResolver>();
    return await resolver.GetEffectiveUserPreferences(tenantId, userId, TestContext.Current.CancellationToken);
  }

  private async Task<bool> ResolveNotifyUser(Guid tenantId, Guid userId)
  {
    await using var scope = _testApp.Services.CreateAsyncScope();
    var resolver = scope.ServiceProvider.GetRequiredService<IEffectiveUserPreferencesResolver>();
    return await resolver.GetNotifyUserOnSessionStart(tenantId, userId, TestContext.Current.CancellationToken);
  }

  private async Task SeedTenantSetting(Guid tenantId, string value)
  {
    await using var scope = _testApp.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDb>();

    db.TenantSettings.Add(new TenantSetting
    {
      Name = TenantSettingNames.NotifyUserOnSessionStart,
      TenantId = tenantId,
      Value = value
    });

    await db.SaveChangesAsync(TestContext.Current.CancellationToken);
  }

  private async Task SeedUserPreference(Guid userId, string value)
  {
    await using var scope = _testApp.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDb>();

    db.UserPreferences.Add(new UserPreference
    {
      Name = UserPreferenceNames.NotifyUserOnSessionStart,
      UserId = userId,
      Value = value
    });

    await db.SaveChangesAsync(TestContext.Current.CancellationToken);
  }
}