using ControlR.Libraries.Api.Contracts.Settings;
using ControlR.Web.Server.Api.V1;
using ControlR.Web.Server.Data;
using ControlR.Web.Server.Services.Settings;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using SettingsDtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.TenantSettings;

namespace ControlR.Web.Server.Tests.V1;

/// <summary>
/// Tenant settings on the V1 controller: required-tenantId resolution, the typed aggregate
/// round trip through the manager, validation problems, the unset-name 204, and delete.
/// </summary>
public class TenantSettingsV1ControllerTests(ITestOutputHelper testOutput)
{
  private readonly ITestOutputHelper _testOutput = testOutput;

  [Fact]
  public async Task DeleteSetting_WhenSet_RemovesSetting()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var (controller, tenant, _) = await scope.CreateControllerWithTestData<TenantSettingsController>(
      userEmail: "ts-delete@test.local",
      presets: PermissionPresets.TenantAdministrator);

    var manager = services.GetRequiredService<ITenantSettingsManager>();
    await controller.SetSetting(
      manager,
      tenant.Id,
      new SettingsDtos.TenantSettingRequestDto(
        TenantSettingNames.NotifyUserOnSessionStart,
        TenantSettingDefinitions.FormatValue(TenantSettingNames.NotifyUserOnSessionStart, true) ?? "true"));

    var deleteResult = await controller.DeleteSetting(
      services.GetRequiredService<AppDb>(),
      TenantSettingNames.NotifyUserOnSessionStart,
      tenant.Id);
    Assert.IsType<NoContentResult>(deleteResult);

    var getResult = await controller.GetAll(manager, tenant.Id, CancellationToken.None);
    var settingsOk = Assert.IsType<OkObjectResult>(getResult.Result);
    var settings = Assert.IsType<SettingsDtos.TenantSettingsDto>(settingsOk.Value);
    Assert.Null(settings.NotifyUserOnSessionStart);
  }

  [Fact]
  public async Task GetAll_WhenCallerRequestsAnotherTenant_Forbids()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var (controller, _, _) = await scope.CreateControllerWithTestData<TenantSettingsController>(
      userEmail: "ts-forbid@test.local",
      presets: PermissionPresets.TenantAdministrator);

    var foreignTenant = await services.CreateTestTenant("TS Foreign");

    var result = await controller.GetAll(
      services.GetRequiredService<ITenantSettingsManager>(),
      foreignTenant.Id,
      CancellationToken.None);

    Assert.IsType<ForbidResult>(result.Result);
  }

  [Fact]
  public async Task GetAll_WhenNoSettingsAreSet_ReturnsNullMembers()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var (controller, tenant, _) = await scope.CreateControllerWithTestData<TenantSettingsController>(
      userEmail: "ts-getall@test.local",
      presets: PermissionPresets.TenantAdministrator);

    var result = await controller.GetAll(
      services.GetRequiredService<ITenantSettingsManager>(),
      tenant.Id,
      CancellationToken.None);

    var ok = Assert.IsType<OkObjectResult>(result.Result);
    var settings = Assert.IsType<SettingsDtos.TenantSettingsDto>(ok.Value);
    Assert.Null(settings.AppendInstanceId);
    Assert.Null(settings.NotifyUserOnSessionStart);
  }

  [Fact]
  public async Task GetSetting_WhenSet_ReturnsValue()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var (controller, tenant, _) = await scope.CreateControllerWithTestData<TenantSettingsController>(
      userEmail: "ts-getset@test.local",
      presets: PermissionPresets.TenantAdministrator);

    var manager = services.GetRequiredService<ITenantSettingsManager>();
    await controller.SetSetting(
      manager,
      tenant.Id,
      new SettingsDtos.TenantSettingRequestDto(
        TenantSettingNames.NotifyUserOnSessionStart,
        TenantSettingDefinitions.FormatValue(TenantSettingNames.NotifyUserOnSessionStart, true) ?? "true"));

    var result = await controller.GetSetting(
      services.GetRequiredService<AppDb>(),
      TenantSettingNames.NotifyUserOnSessionStart,
      tenant.Id);

    var dto = Assert.IsType<SettingsDtos.TenantSettingResponseDto>(result.Value);
    Assert.Equal(TenantSettingNames.NotifyUserOnSessionStart, dto.Name);
  }

  [Fact]
  public async Task GetSetting_WhenUnset_ReturnsNoContent()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var (controller, tenant, _) = await scope.CreateControllerWithTestData<TenantSettingsController>(
      userEmail: "ts-getunset@test.local",
      presets: PermissionPresets.TenantAdministrator);

    var result = await controller.GetSetting(
      services.GetRequiredService<AppDb>(),
      TenantSettingNames.InstanceId,
      tenant.Id);

    Assert.IsType<NoContentResult>(result.Result);
  }

  [Fact]
  public async Task SetSettings_UpdatesAggregateAndReturnsIt()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var (controller, tenant, _) = await scope.CreateControllerWithTestData<TenantSettingsController>(
      userEmail: "ts-put@test.local",
      presets: PermissionPresets.TenantAdministrator);

    var result = await controller.SetSettings(
      services.GetRequiredService<ITenantSettingsManager>(),
      tenant.Id,
      new SettingsDtos.TenantSettingsDto(true, null, true),
      CancellationToken.None);

    var ok = Assert.IsType<OkObjectResult>(result.Result);
    var settings = Assert.IsType<SettingsDtos.TenantSettingsDto>(ok.Value);
    Assert.True(settings.AppendInstanceId);
    Assert.True(settings.NotifyUserOnSessionStart);
  }

  [Fact]
  public async Task SetSetting_RoundTripsThroughGetAll()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var (controller, tenant, _) = await scope.CreateControllerWithTestData<TenantSettingsController>(
      userEmail: "ts-set@test.local",
      presets: PermissionPresets.TenantAdministrator);

    var manager = services.GetRequiredService<ITenantSettingsManager>();
    var result = await controller.SetSetting(
      manager,
      tenant.Id,
      new SettingsDtos.TenantSettingRequestDto(
        TenantSettingNames.NotifyUserOnSessionStart,
        TenantSettingDefinitions.FormatValue(TenantSettingNames.NotifyUserOnSessionStart, true) ?? "true"));

    var ok = Assert.IsType<OkObjectResult>(result.Result);
    var dto = Assert.IsType<SettingsDtos.TenantSettingResponseDto>(ok.Value);
    Assert.Equal(TenantSettingNames.NotifyUserOnSessionStart, dto.Name);

    var getResult = await controller.GetAll(manager, tenant.Id, CancellationToken.None);
    var settingsOk = Assert.IsType<OkObjectResult>(getResult.Result);
    var settings = Assert.IsType<SettingsDtos.TenantSettingsDto>(settingsOk.Value);
    Assert.True(settings.NotifyUserOnSessionStart);
  }

  [Fact]
  public async Task SetSetting_WhenValueInvalid_ReturnsValidationProblem()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var (controller, tenant, _) = await scope.CreateControllerWithTestData<TenantSettingsController>(
      userEmail: "ts-invalid@test.local",
      presets: PermissionPresets.TenantAdministrator);

    var result = await controller.SetSetting(
      services.GetRequiredService<ITenantSettingsManager>(),
      tenant.Id,
      new SettingsDtos.TenantSettingRequestDto(TenantSettingNames.NotifyUserOnSessionStart, "maybe"));

    var problem = Assert.IsType<ObjectResult>(result.Result);
    Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
  }
}
