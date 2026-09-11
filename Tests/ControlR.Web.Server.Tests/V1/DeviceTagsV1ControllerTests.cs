using ControlR.Web.Server.Api.V1;
using ControlR.Web.Server.Data;
using ControlR.Web.Server.Data.Entities;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using DeviceTagsDtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.DeviceTags;

namespace ControlR.Web.Server.Tests.V1;

/// <summary>
/// Device-tag association on the V1 controller: device-scoped tags.write authorization,
/// cross-tenant device/tag invisibility, and the association round trip.
/// </summary>
public class DeviceTagsV1ControllerTests(ITestOutputHelper testOutput)
{
  private readonly ITestOutputHelper _testOutput = testOutput;

  [Fact]
  public async Task Add_Removes_RoundTripsAssociation()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var (controller, tenant, _) = await scope.CreateControllerWithTestData<DeviceTagsController>(
      userEmail: "devicetags-roundtrip@test.local",
      presets: PermissionPresets.DeviceSuperUser);

    var device = await services.CreateTestDevice(tenant.Id);
    var tag = await CreateTagAsync(testApp, tenant.Id, "roundtrip");
    var authorizationService = services.GetRequiredService<IAuthorizationService>();

    var addResult = await controller.Add(
      services.GetRequiredService<AppDb>(),
      authorizationService,
      tenant.Id,
      new DeviceTagsDtos.DeviceTagAddRequestDto(device.Id, tag.Id),
      TestContext.Current.CancellationToken);
    Assert.IsType<NoContentResult>(addResult);

    await using (var appDb = services.GetRequiredService<AppDb>())
    {
      var linked = await appDb.Devices
        .Include(x => x.Tags)
        .FirstAsync(x => x.Id == device.Id, TestContext.Current.CancellationToken);
      Assert.Contains(linked.Tags!, x => x.Id == tag.Id);
    }

    var removeResult = await controller.Remove(
      services.GetRequiredService<AppDb>(),
      authorizationService,
      device.Id,
      tag.Id,
      tenant.Id,
      TestContext.Current.CancellationToken);
    Assert.IsType<NoContentResult>(removeResult);

    await using (var appDb = services.GetRequiredService<AppDb>())
    {
      var linked = await appDb.Devices
        .Include(x => x.Tags)
        .FirstAsync(x => x.Id == device.Id, TestContext.Current.CancellationToken);
      Assert.DoesNotContain(linked.Tags ?? [], x => x.Id == tag.Id);
    }
  }

  [Fact]
  public async Task Add_WhenDeviceInOtherTenant_ReturnsNotFound()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var (controller, tenantA, _) = await scope.CreateControllerWithTestData<DeviceTagsController>(
      userEmail: "devicetags-a@test.local",
      presets: PermissionPresets.DeviceSuperUser);

    var tenantB = await testApp.Services.CreateTestTenant("DeviceTags B");
    var foreignDevice = await services.CreateTestDevice(tenantB.Id);
    var tag = await CreateTagAsync(testApp, tenantA.Id, "mine");

    var result = await controller.Add(
      services.GetRequiredService<AppDb>(),
      services.GetRequiredService<IAuthorizationService>(),
      tenantA.Id,
      new DeviceTagsDtos.DeviceTagAddRequestDto(foreignDevice.Id, tag.Id),
      TestContext.Current.CancellationToken);

    Assert.IsType<NotFoundObjectResult>(result);
  }

  [Fact]
  public async Task Add_WithoutTagsWritePolicy_Forbids()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var (controller, tenant, _) = await scope.CreateControllerWithTestData<DeviceTagsController>(
      userEmail: "devicetags-forbid@test.local",
      presets: PermissionPresets.TenantAdministrator);

    var device = await services.CreateTestDevice(tenant.Id);
    var tag = await CreateTagAsync(testApp, tenant.Id, "noperm");

    var result = await controller.Add(
      services.GetRequiredService<AppDb>(),
      services.GetRequiredService<IAuthorizationService>(),
      tenant.Id,
      new DeviceTagsDtos.DeviceTagAddRequestDto(device.Id, tag.Id),
      TestContext.Current.CancellationToken);

    Assert.IsType<ForbidResult>(result);
  }

  [Fact]
  public async Task Remove_WhenTagNotOnDevice_ReturnsNotFound()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var (controller, tenant, _) = await scope.CreateControllerWithTestData<DeviceTagsController>(
      userEmail: "devicetags-notlinked@test.local",
      presets: PermissionPresets.DeviceSuperUser);

    var device = await services.CreateTestDevice(tenant.Id);
    var tag = await CreateTagAsync(testApp, tenant.Id, "unlinked");

    var result = await controller.Remove(
      services.GetRequiredService<AppDb>(),
      services.GetRequiredService<IAuthorizationService>(),
      device.Id,
      tag.Id,
      tenant.Id,
      TestContext.Current.CancellationToken);

    Assert.IsType<NotFoundObjectResult>(result);
  }

  private static async Task<Tag> CreateTagAsync(TestApp testApp, Guid tenantId, string name)
  {
    using var scope = testApp.CreateScope();
    await using var appDb = scope.ServiceProvider.GetRequiredService<AppDb>();
    var tag = new Tag { Name = name, TenantId = tenantId, Type = TagType.Permission };
    appDb.Tags.Add(tag);
    await appDb.SaveChangesAsync(TestContext.Current.CancellationToken);
    return tag;
  }
}
