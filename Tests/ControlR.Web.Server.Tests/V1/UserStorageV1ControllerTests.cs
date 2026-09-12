using ControlR.Web.Server.Api.V1;
using ControlR.Web.Server.Services.Settings;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.UserStorage;

namespace ControlR.Web.Server.Tests.V1;

/// <summary>
/// Per-user key-value storage on the V1 controller: caller-owned items, required-tenantId
/// gate, the unset-key 204 on get, and 204/404 semantics on delete.
/// </summary>
public class UserStorageV1ControllerTests(ITestOutputHelper testOutput)
{
  private readonly ITestOutputHelper _testOutput = testOutput;

  [Fact]
  public async Task DeleteItem_WhenSet_RemovesItem()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var (controller, tenant, _) = await scope.CreateControllerWithTestData<UserStorageController>(
      userEmail: "us-delete@test.local",
      presets: PermissionPresets.TenantAdministrator);

    var manager = services.GetRequiredService<IUserStorageManager>();
    await controller.SetItem(
      manager,
      tenant.Id,
      new UserStorageRequestDto("doomed-key", "x"),
      CancellationToken.None);

    var deleteResult = await controller.DeleteItem(manager, "doomed-key", tenant.Id, CancellationToken.None);
    Assert.IsType<NoContentResult>(deleteResult);

    var getResult = await controller.GetItem(manager, "doomed-key", tenant.Id, CancellationToken.None);
    Assert.IsType<NoContentResult>(getResult.Result);
  }

  [Fact]
  public async Task DeleteItem_WhenUnknown_ReturnsNotFound()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var (controller, tenant, _) = await scope.CreateControllerWithTestData<UserStorageController>(
      userEmail: "us-unknowndelete@test.local",
      presets: PermissionPresets.TenantAdministrator);

    var result = await controller.DeleteItem(
      services.GetRequiredService<IUserStorageManager>(),
      "never-set",
      tenant.Id,
      CancellationToken.None);

    Assert.IsType<NotFoundResult>(result);
  }

  [Fact]
  public async Task GetItem_WhenUnset_ReturnsNoContent()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var (controller, tenant, _) = await scope.CreateControllerWithTestData<UserStorageController>(
      userEmail: "us-unset@test.local",
      presets: PermissionPresets.TenantAdministrator);

    var result = await controller.GetItem(
      services.GetRequiredService<IUserStorageManager>(),
      "missing-key",
      tenant.Id,
      CancellationToken.None);

    Assert.IsType<NoContentResult>(result.Result);
  }

  [Fact]
  public async Task SetItem_RoundTripsThroughGetItem()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var (controller, tenant, _) = await scope.CreateControllerWithTestData<UserStorageController>(
      userEmail: "us-roundtrip@test.local",
      presets: PermissionPresets.TenantAdministrator);

    var manager = services.GetRequiredService<IUserStorageManager>();
    var setResult = await controller.SetItem(
      manager,
      tenant.Id,
      new UserStorageRequestDto("ack-version", "1.2.3"),
      CancellationToken.None);

    var setOk = Assert.IsType<OkObjectResult>(setResult.Result);
    var setDto = Assert.IsType<UserStorageResponseDto>(setOk.Value);
    Assert.Equal("1.2.3", setDto.Value);

    var getResult = await controller.GetItem(manager, "ack-version", tenant.Id, CancellationToken.None);
    var getOk = Assert.IsType<OkObjectResult>(getResult.Result);
    var getDto = Assert.IsType<UserStorageResponseDto>(getOk.Value);
    Assert.Equal("ack-version", getDto.Key);
    Assert.Equal("1.2.3", getDto.Value);
  }

  [Fact]
  public async Task SetItem_WhenCallerRequestsAnotherTenant_Forbids()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var (controller, _, _) = await scope.CreateControllerWithTestData<UserStorageController>(
      userEmail: "us-forbid@test.local",
      presets: PermissionPresets.TenantAdministrator);

    var foreignTenant = await services.CreateTestTenant("US Foreign");

    var result = await controller.SetItem(
      services.GetRequiredService<IUserStorageManager>(),
      foreignTenant.Id,
      new UserStorageRequestDto("stray-key", "v"),
      CancellationToken.None);

    Assert.IsType<ForbidResult>(result.Result);
  }
}
