using ControlR.Web.Server.Api.V1;
using ControlR.Web.Server.Authz.Permissions;
using ControlR.Web.Server.Data;
using ControlR.Web.Server.Data.Entities;
using ControlR.Web.Server.Services.DeviceManagement;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TagsDtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.Tags;

namespace ControlR.Web.Server.Tests.V1;

/// <summary>
/// Tag CRUD on the V1 controller: required-tenantId resolution, cross-tenant invisibility,
/// and the device-readable filtering that keeps tag linkage from bypassing device read scope.
/// </summary>
public class TagsV1ControllerTests(ITestOutputHelper testOutput)
{
  private readonly ITestOutputHelper _testOutput = testOutput;

  [Fact]
  public async Task Create_ReturnsCreatedTag()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var (controller, tenant, _) = await scope.CreateControllerWithTestData<TagsController>(
      userEmail: "tags-create@test.local",
      presets: PermissionPresets.TenantAdministrator);

    var result = await controller.Create(
      scope.ServiceProvider.GetRequiredService<AppDb>(),
      tenant.Id,
      new TagsDtos.TagCreateRequestDto("prod", TagType.Permission),
      TestContext.Current.CancellationToken);

    var created = Assert.IsType<CreatedAtActionResult>(result.Result);
    var dto = Assert.IsType<TagsDtos.TagResponseDto>(created.Value);
    Assert.Equal("prod", dto.Name);
    Assert.Empty(dto.DeviceIds);
  }

  [Fact]
  public async Task Create_WhenCallerRequestsAnotherTenant_Forbids()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var (controller, _, _) = await scope.CreateControllerWithTestData<TagsController>(
      userEmail: "tags-create-bad@test.local",
      presets: PermissionPresets.TenantAdministrator);

    var foreignTenant = await testApp.Services.CreateTestTenant("Foreign Tags");

    var result = await controller.Create(
      scope.ServiceProvider.GetRequiredService<AppDb>(),
      foreignTenant.Id,
      new TagsDtos.TagCreateRequestDto("stray", TagType.Permission),
      TestContext.Current.CancellationToken);

    Assert.IsType<ForbidResult>(result.Result);
  }

  [Fact]
  public async Task Delete_FromOtherTenant_ReturnsNotFound()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var (controller, tenantA, _) = await scope.CreateControllerWithTestData<TagsController>(
      userEmail: "tags-delete-a@test.local",
      presets: PermissionPresets.TenantAdministrator);

    var tenantB = await testApp.Services.CreateTestTenant("Tags Tenant B");
    var foreignTag = await CreateTagAsync(testApp, tenantB.Id, "Foreign");

    var result = await controller.Delete(
      scope.ServiceProvider.GetRequiredService<AppDb>(),
      foreignTag.Id,
      tenantA.Id,
      TestContext.Current.CancellationToken);

    Assert.IsType<NotFoundResult>(result);
  }

  [Fact]
  public async Task Delete_RemovesTag()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var (controller, tenant, _) = await scope.CreateControllerWithTestData<TagsController>(
      userEmail: "tags-delete@test.local",
      presets: PermissionPresets.TenantAdministrator);

    var tag = await CreateTagAsync(testApp, tenant.Id, "Doomed");

    var result = await controller.Delete(
      scope.ServiceProvider.GetRequiredService<AppDb>(),
      tag.Id,
      tenant.Id,
      TestContext.Current.CancellationToken);

    Assert.IsType<NoContentResult>(result);

    await using (var appDb = scope.ServiceProvider.GetRequiredService<AppDb>())
    {
      var remaining = await appDb.Tags.CountAsync(
        x => x.Id == tag.Id, TestContext.Current.CancellationToken);
      Assert.Equal(0, remaining);
    }
  }

  [Fact]
  public async Task GetAll_ExcludesOtherTenantsTags()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();

    var (controller, tenantA, _) = await scope.CreateControllerWithTestData<TagsController>(
      userEmail: "tags-isolate-a@test.local",
      presets: PermissionPresets.TenantAdministrator);

    await CreateTagAsync(testApp, tenantA.Id, "Mine");
    var tenantB = await testApp.Services.CreateTestTenant("Tags Isolation B");
    await CreateTagAsync(testApp, tenantB.Id, "Theirs");

    var result = await controller.GetAll(
      scope.ServiceProvider.GetRequiredService<AppDb>(),
      scope.ServiceProvider.GetRequiredService<IDeviceAccessScopeResolver>(),
      tenantA.Id,
      cancellationToken: TestContext.Current.CancellationToken);

    var ok = Assert.IsType<OkObjectResult>(result.Result);
    var response = Assert.IsType<TagsDtos.TagsResponseDto>(ok.Value);

    Assert.Contains(response.Items, x => x.Name == "Mine");
    Assert.DoesNotContain(response.Items, x => x.Name == "Theirs");
  }

  [Fact]
  public async Task GetAll_WithLinkedIds_OnlyExposesReadableDevices()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;

    // The user gets device.read for exactly one device. The tag must not leak the other id.
    var (controller, tenant, user) = await scope.CreateControllerWithTestData<TagsController>(
      userEmail: "tags-all@test.local");

    var deviceOne = await services.CreateTestDevice(tenant.Id);
    var deviceTwo = await services.CreateTestDevice(tenant.Id);

    using (var grantScope = services.CreateScope())
    {
      await using var grantDb = grantScope.ServiceProvider.GetRequiredService<AppDb>();
      grantDb.PermissionAssignments.Add(PermissionAssignment.CreateGrant(
        PermissionPrincipalKind.User,
        user.Id,
        PermissionNames.DeviceRead,
        PermissionScopeKind.Device,
        scopeId: deviceOne.Id,
        owningTenantId: tenant.Id,
        createdBy: null));
      await grantDb.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    await using (var appDb = services.GetRequiredService<AppDb>())
    {
      var tag = new Tag { Name = "linked", TenantId = tenant.Id, Type = TagType.Permission };
      appDb.Tags.Add(tag);
      await appDb.SaveChangesAsync(TestContext.Current.CancellationToken);

      var devices = await appDb.Devices
        .Where(x => x.Id == deviceOne.Id || x.Id == deviceTwo.Id)
        .ToListAsync(TestContext.Current.CancellationToken);
      tag.Devices = [.. devices];
      await appDb.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    var result = await controller.GetAll(
      services.GetRequiredService<AppDb>(),
      services.GetRequiredService<IDeviceAccessScopeResolver>(),
      tenant.Id,
      includeLinkedIds: true,
      cancellationToken: TestContext.Current.CancellationToken);

    var ok = Assert.IsType<OkObjectResult>(result.Result);
    var response = Assert.IsType<TagsDtos.TagsResponseDto>(ok.Value);

    var tagDto = Assert.Single(response.Items, x => x.Name == "linked");
    Assert.Contains(deviceOne.Id, tagDto.DeviceIds);
    Assert.DoesNotContain(deviceTwo.Id, tagDto.DeviceIds);
  }

  [Fact]
  public async Task Get_FromOtherTenant_ReturnsNotFound()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var (controller, tenantA, _) = await scope.CreateControllerWithTestData<TagsController>(
      userEmail: "tags-get-a@test.local",
      presets: PermissionPresets.TenantAdministrator);

    var tenantB = await testApp.Services.CreateTestTenant("Tags Get B");
    var foreignTag = await CreateTagAsync(testApp, tenantB.Id, "Foreign");

    var result = await controller.Get(
      scope.ServiceProvider.GetRequiredService<AppDb>(),
      scope.ServiceProvider.GetRequiredService<IDeviceAccessScopeResolver>(),
      foreignTag.Id,
      tenantA.Id,
      TestContext.Current.CancellationToken);

    Assert.IsType<NotFoundResult>(result.Result);
  }

  [Fact]
  public async Task Update_RenamesTag()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var (controller, tenant, _) = await scope.CreateControllerWithTestData<TagsController>(
      userEmail: "tags-rename@test.local",
      presets: PermissionPresets.TenantAdministrator);

    var tag = await CreateTagAsync(testApp, tenant.Id, "old-name");

    var result = await controller.Update(
      scope.ServiceProvider.GetRequiredService<AppDb>(),
      tag.Id,
      tenant.Id,
      new TagsDtos.UpdateTagRequestDto("new-name"),
      TestContext.Current.CancellationToken);

    var ok = Assert.IsType<OkObjectResult>(result.Result);
    var dto = Assert.IsType<TagsDtos.TagResponseDto>(ok.Value);
    Assert.Equal("new-name", dto.Name);
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
