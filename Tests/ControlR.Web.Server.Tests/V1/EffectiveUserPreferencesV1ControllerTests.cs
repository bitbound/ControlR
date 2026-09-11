using ControlR.Libraries.Api.Contracts.Settings;
using ControlR.Web.Server.Api.V1;
using ControlR.Web.Server.Services.Settings;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using EffectiveDtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.EffectiveUserPreferences;

namespace ControlR.Web.Server.Tests.V1;

/// <summary>
/// Effective user preferences on the V1 controller: the caller's user preference answers by
/// default, a tenant setting overrides it and flips the enforcement flag, and the required
/// tenantId gate rejects foreign tenants.
/// </summary>
public class EffectiveUserPreferencesV1ControllerTests(ITestOutputHelper testOutput)
{
  private readonly ITestOutputHelper _testOutput = testOutput;

  [Fact]
  public async Task GetAll_WhenCallerRequestsAnotherTenant_Forbids()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var (controller, _, _) = await scope.CreateControllerWithTestData<EffectiveUserPreferencesController>(
      userEmail: "eff-forbid@test.local",
      presets: PermissionPresets.TenantAdministrator);

    var foreignTenant = await services.CreateTestTenant("EFF Foreign");

    var result = await controller.GetAll(
      services.GetRequiredService<IEffectiveUserPreferencesResolver>(),
      foreignTenant.Id,
      CancellationToken.None);

    Assert.IsType<ForbidResult>(result.Result);
  }

  [Fact]
  public async Task GetAll_WhenTenantSettingIsSet_ReturnsTenantOverride()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var (controller, tenant, _) = await scope.CreateControllerWithTestData<EffectiveUserPreferencesController>(
      userEmail: "eff-override@test.local",
      presets: PermissionPresets.TenantAdministrator);

    var settingResult = await services.GetRequiredService<ITenantSettingsManager>().SetSetting(
      tenant.Id,
      new InternalDtos.TenantSettingRequestDto(TenantSettingNames.NotifyUserOnSessionStart, "false"),
      TestContext.Current.CancellationToken);
    Assert.True(settingResult.IsSuccess);

    var result = await controller.GetAll(
      services.GetRequiredService<IEffectiveUserPreferencesResolver>(),
      tenant.Id,
      CancellationToken.None);

    var ok = Assert.IsType<OkObjectResult>(result.Result);
    var preferences = Assert.IsType<EffectiveDtos.EffectiveUserPreferencesDto>(ok.Value);
    Assert.False(preferences.NotifyUserOnSessionStart);
    Assert.True(preferences.IsNotifyUserOnSessionStartTenantEnforced);
  }

  [Fact]
  public async Task GetAll_WithoutTenantOverride_UsesUserPreferenceDefault()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var (controller, tenant, _) = await scope.CreateControllerWithTestData<EffectiveUserPreferencesController>(
      userEmail: "eff-default@test.local",
      presets: PermissionPresets.TenantAdministrator);

    var result = await controller.GetAll(
      services.GetRequiredService<IEffectiveUserPreferencesResolver>(),
      tenant.Id,
      CancellationToken.None);

    var ok = Assert.IsType<OkObjectResult>(result.Result);
    var preferences = Assert.IsType<EffectiveDtos.EffectiveUserPreferencesDto>(ok.Value);
    Assert.True(preferences.NotifyUserOnSessionStart);
    Assert.False(preferences.IsNotifyUserOnSessionStartTenantEnforced);
  }
}
