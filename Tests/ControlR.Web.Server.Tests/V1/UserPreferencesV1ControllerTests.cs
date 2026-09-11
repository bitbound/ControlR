using ControlR.Libraries.Api.Contracts.Settings;
using ControlR.Web.Server.Api.V1;
using ControlR.Web.Server.Data;
using ControlR.Web.Server.Services.Settings;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using PrefsDtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.UserPreferences;

namespace ControlR.Web.Server.Tests.V1;

/// <summary>
/// Self-service user preferences on the V1 controller: caller-owned reads/writes, the
/// required-tenantId gate, defaults for a fresh user, and the unset-name 204.
/// </summary>
public class UserPreferencesV1ControllerTests(ITestOutputHelper testOutput)
{
  private readonly ITestOutputHelper _testOutput = testOutput;

  [Fact]
  public async Task GetAll_ForFreshUser_ReturnsDefaults()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var (controller, tenant, _) = await scope.CreateControllerWithTestData<UserPreferencesController>(
      userEmail: "up-defaults@test.local",
      presets: PermissionPresets.TenantAdministrator);

    var result = await controller.GetAll(
      services.GetRequiredService<IUserPreferencesManager>(),
      tenant.Id,
      CancellationToken.None);

    var ok = Assert.IsType<OkObjectResult>(result.Result);
    var preferences = Assert.IsType<PrefsDtos.UserPreferencesDto>(ok.Value);
    Assert.True(preferences.NotifyUserOnSessionStart);
    Assert.True(preferences.HideOfflineDevices);
  }

  [Fact]
  public async Task GetAll_WhenCallerRequestsAnotherTenant_Forbids()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var (controller, _, _) = await scope.CreateControllerWithTestData<UserPreferencesController>(
      userEmail: "up-forbid@test.local",
      presets: PermissionPresets.TenantAdministrator);

    var foreignTenant = await services.CreateTestTenant("UP Foreign");

    var result = await controller.GetAll(
      services.GetRequiredService<IUserPreferencesManager>(),
      foreignTenant.Id,
      CancellationToken.None);

    Assert.IsType<ForbidResult>(result.Result);
  }

  [Fact]
  public async Task GetPreference_WhenSet_ReturnsValue()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var (controller, tenant, _) = await scope.CreateControllerWithTestData<UserPreferencesController>(
      userEmail: "up-getset@test.local",
      presets: PermissionPresets.TenantAdministrator);

    var manager = services.GetRequiredService<IUserPreferencesManager>();
    await controller.SetPreference(
      manager,
      tenant.Id,
      new PrefsDtos.UserPreferenceRequestDto(UserPreferenceNames.ThemeMode, UserPreferenceDefinitions.FormatValue(UserPreferenceNames.ThemeMode, ThemeMode.Dark) ?? "dark"),
      CancellationToken.None);

    var result = await controller.GetPreference(
      services.GetRequiredService<AppDb>(),
      UserPreferenceNames.ThemeMode,
      tenant.Id);

    var dto = Assert.IsType<PrefsDtos.UserPreferenceResponseDto>(result.Value);
    Assert.Equal(UserPreferenceNames.ThemeMode, dto.Name);
  }

  [Fact]
  public async Task GetPreference_WhenUnset_ReturnsNoContent()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var (controller, tenant, _) = await scope.CreateControllerWithTestData<UserPreferencesController>(
      userEmail: "up-getunset@test.local",
      presets: PermissionPresets.TenantAdministrator);

    var result = await controller.GetPreference(
      services.GetRequiredService<AppDb>(),
      UserPreferenceNames.ThemeMode,
      tenant.Id);

    Assert.IsType<NoContentResult>(result.Result);
  }

  [Fact]
  public async Task SetPreferences_ReplacesAggregateAndReturnsIt()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var (controller, tenant, _) = await scope.CreateControllerWithTestData<UserPreferencesController>(
      userEmail: "up-put@test.local",
      presets: PermissionPresets.TenantAdministrator);

    var manager = services.GetRequiredService<IUserPreferencesManager>();
    var currentResult = await controller.GetAll(manager, tenant.Id, CancellationToken.None);
    var currentOk = Assert.IsType<OkObjectResult>(currentResult.Result);
    var current = Assert.IsType<PrefsDtos.UserPreferencesDto>(currentOk.Value);

    var result = await controller.SetPreferences(
      manager,
      tenant.Id,
      current with { ThemeMode = ThemeMode.Light, HideOfflineDevices = false },
      CancellationToken.None);

    var ok = Assert.IsType<OkObjectResult>(result.Result);
    var preferences = Assert.IsType<PrefsDtos.UserPreferencesDto>(ok.Value);
    Assert.Equal(ThemeMode.Light, preferences.ThemeMode);
    Assert.False(preferences.HideOfflineDevices);
  }

  [Fact]
  public async Task SetPreference_RoundTripsThroughGetAll()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var (controller, tenant, _) = await scope.CreateControllerWithTestData<UserPreferencesController>(
      userEmail: "up-set@test.local",
      presets: PermissionPresets.TenantAdministrator);

    var manager = services.GetRequiredService<IUserPreferencesManager>();
    var themeModeValue = UserPreferenceDefinitions.FormatValue(UserPreferenceNames.ThemeMode, ThemeMode.Dark) ?? "dark";

    var setResult = await controller.SetPreference(
      manager,
      tenant.Id,
      new PrefsDtos.UserPreferenceRequestDto(UserPreferenceNames.ThemeMode, themeModeValue),
      CancellationToken.None);

    var ok = Assert.IsType<OkObjectResult>(setResult.Result);
    var response = Assert.IsType<PrefsDtos.UserPreferenceResponseDto>(ok.Value);
    Assert.Equal(UserPreferenceNames.ThemeMode, response.Name);

    var getResult = await controller.GetAll(manager, tenant.Id, CancellationToken.None);
    var allOk = Assert.IsType<OkObjectResult>(getResult.Result);
    var preferences = Assert.IsType<PrefsDtos.UserPreferencesDto>(allOk.Value);
    Assert.Equal(ThemeMode.Dark, preferences.ThemeMode);
  }

  [Fact]
  public async Task SetPreference_WhenValueInvalid_ReturnsValidationProblem()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var (controller, tenant, _) = await scope.CreateControllerWithTestData<UserPreferencesController>(
      userEmail: "up-invalid@test.local",
      presets: PermissionPresets.TenantAdministrator);

    var result = await controller.SetPreference(
      services.GetRequiredService<IUserPreferencesManager>(),
      tenant.Id,
      new PrefsDtos.UserPreferenceRequestDto(UserPreferenceNames.ThemeMode, "not-a-theme"),
      CancellationToken.None);

    var problem = Assert.IsType<ObjectResult>(result.Result);
    Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
  }
}
