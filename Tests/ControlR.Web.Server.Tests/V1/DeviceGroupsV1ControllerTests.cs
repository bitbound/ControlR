using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.DeviceGroups;
using ControlR.Web.Server.Api.V1;
using ControlR.Web.Server.Authz.Permissions;
using ControlR.Web.Server.Data;
using ControlR.Web.Server.Data.Entities;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ControlR.Web.Server.Tests.V1;

/// <summary>
/// Tenant isolation, group-scoped membership authorization, and lifecycle behavior of the
/// V1 device-groups endpoints. Direct controller calls bypass endpoint policies (authorization
/// middleware), so these exercise the in-handler tenant resolution, the manager's explicit
/// TenantId scoping, and the group-scoped device-group.assign-devices evaluation.
/// </summary>
public class DeviceGroupsV1ControllerTests(ITestOutputHelper testOutput)
{
  private readonly ITestOutputHelper _testOutput = testOutput;

  [Fact]
  public async Task AddAndRemoveMembers_RoundTripsMembership()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;

    var (controller, tenant, _) = await scope.CreateControllerWithTestData<DeviceGroupsController>(
      "Member Tenant",
      "memberadmin@test.local",
      PermissionPresets.TenantAdministrator);
    var groupId = await CreateGroupAsync(controller, tenant.Id, "Membership Group");
    var memberDevice = await services.CreateTestDevice(tenant.Id);

    var authorizationService = services.GetRequiredService<IAuthorizationService>();
    var addResult = await controller.AddMembers(
      groupId,
      tenant.Id,
      new AddDeviceGroupMembersRequestDto([memberDevice.Id]),
      authorizationService,
      TestContext.Current.CancellationToken);
    Assert.IsType<NoContentResult>(addResult);

    var removeResult = await controller.RemoveMembers(
      groupId,
      tenant.Id,
      new RemoveDeviceGroupMembersRequestDto([memberDevice.Id]),
      authorizationService,
      TestContext.Current.CancellationToken);
    Assert.IsType<NoContentResult>(removeResult);

    var getResult = await controller.Get(groupId, tenant.Id, TestContext.Current.CancellationToken);
    var dto = Assert.IsType<DeviceGroupDetailDto>(Assert.IsType<OkObjectResult>(getResult.Result!).Value);
    Assert.Empty(dto.Members);
  }

  [Fact]
  public async Task AddMembers_WithGroupScopedPermission_AuthorizesOnlyTargetGroup()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;

    var (controller, tenant, user) = await scope.CreateControllerWithTestData<DeviceGroupsController>(
      "Member Tenant",
      "groupadmin@test.local",
      PermissionPresets.TenantAdministrator);

    var memberDevice = await services.CreateTestDevice(tenant.Id);
    var authorizedGroupId = await CreateGroupAsync(controller, tenant.Id, "Authorized Group");
    var unauthorizedGroupId = await CreateGroupAsync(controller, tenant.Id, "Unauthorized Group");

    await ReplaceGroupAssignment(
      services,
      user.Id,
      tenant.Id,
      PermissionNames.DeviceGroupAssignDevices,
      PermissionScopeKind.DeviceGroup,
      authorizedGroupId);

    var authorizationService = services.GetRequiredService<IAuthorizationService>();

    var authorizedResult = await controller.AddMembers(
      authorizedGroupId,
      tenant.Id,
      new AddDeviceGroupMembersRequestDto([memberDevice.Id]),
      authorizationService,
      TestContext.Current.CancellationToken);
    var unauthorizedResult = await controller.AddMembers(
      unauthorizedGroupId,
      tenant.Id,
      new AddDeviceGroupMembersRequestDto([memberDevice.Id]),
      authorizationService,
      TestContext.Current.CancellationToken);

    Assert.IsType<NoContentResult>(authorizedResult);
    Assert.IsType<ForbidResult>(unauthorizedResult);
  }

  [Fact]
  public async Task Create_ReturnsCreatedAtActionResultWithDetailDto()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();

    var (controller, tenant, _) = await scope.CreateControllerWithTestData<DeviceGroupsController>(
      "Create Tenant",
      "creator@test.local",
      PermissionPresets.TenantAdministrator);

    var result = await controller.Create(
      tenant.Id,
      new CreateDeviceGroupRequestDto("Production Servers", "Main production fleet"),
      TestContext.Current.CancellationToken);

    var createdAt = Assert.IsType<CreatedAtActionResult>(result.Result);
    var dto = Assert.IsType<DeviceGroupDetailDto>(createdAt.Value);
    Assert.Equal("Production Servers", dto.Name);
    Assert.Equal("Main production fleet", dto.Description);
    Assert.NotEqual(Guid.Empty, dto.Id);
    Assert.Empty(dto.Members);
  }

  [Fact]
  public async Task Create_WhenCallerRequestsAnotherTenant_Forbids()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();

    var (controller, tenantA, _) = await scope.CreateControllerWithTestData<DeviceGroupsController>(
      "Tenant A",
      "a@test.local");
    var tenantB = await scope.ServiceProvider.CreateTestTenant("Tenant B");

    var result = await controller.Create(
      tenantB.Id,
      new CreateDeviceGroupRequestDto("Stray Group", null),
      TestContext.Current.CancellationToken);

    Assert.IsType<ForbidResult>(result.Result);
  }

  [Fact]
  public async Task Delete_WithServerPrincipal_RemovesAnotherUsersGroup()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();

    var (ownerController, tenant, _) = await scope.CreateControllerWithTestData<DeviceGroupsController>(
      "Delete Tenant",
      "groupowner@test.local",
      PermissionPresets.TenantAdministrator);
    var groupId = await CreateGroupAsync(ownerController, tenant.Id, "Disposable Group");

    var serverController = await scope.CreateControllerWithServerPrincipal<DeviceGroupsController>();
    var deleteResult = await serverController.Delete(groupId, tenant.Id, TestContext.Current.CancellationToken);
    Assert.IsType<NoContentResult>(deleteResult);

    var getResult = await serverController.Get(groupId, tenant.Id, TestContext.Current.CancellationToken);
    ActionResultAsserts.AssertHttpStatus(getResult.Result, StatusCodes.Status404NotFound);
  }

  [Fact]
  public async Task GetAll_WhenCallerRequestsAnotherTenant_Forbids()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();

    var (controller, tenantA, _) = await scope.CreateControllerWithTestData<DeviceGroupsController>(
      "Tenant A",
      "a@test.local");
    var tenantB = await scope.ServiceProvider.CreateTestTenant("Tenant B");

    var result = await controller.GetAll(tenantB.Id, TestContext.Current.CancellationToken);

    Assert.IsType<ForbidResult>(result.Result);
  }

  [Fact]
  public async Task Get_WhenGroupBelongsToAnotherTenant_ReturnsNotFound()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();

    var (ownerController, tenantA, _) = await scope.CreateControllerWithTestData<DeviceGroupsController>(
      "Tenant A",
      "groupowner@test.local",
      PermissionPresets.TenantAdministrator);
    var groupId = await CreateGroupAsync(ownerController, tenantA.Id, "Tenant A Group");
    var tenantB = await scope.ServiceProvider.CreateTestTenant("Tenant B");

    // Server principals trust the requested tenant id. The manager's explicit TenantId
    // predicate must surface the mismatch as NotFound, not expose the cross-tenant group.
    var serverController = await scope.CreateControllerWithServerPrincipal<DeviceGroupsController>();
    var result = await serverController.Get(groupId, tenantB.Id, TestContext.Current.CancellationToken);

    ActionResultAsserts.AssertHttpStatus(result.Result, StatusCodes.Status404NotFound);
  }

  [Fact]
  public async Task Update_ReturnsUpdatedDetailDto()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();

    var (controller, tenant, _) = await scope.CreateControllerWithTestData<DeviceGroupsController>(
      "Update Tenant",
      "updater@test.local",
      PermissionPresets.TenantAdministrator);
    var groupId = await CreateGroupAsync(controller, tenant.Id, "Staging");

    var result = await controller.Update(
      groupId,
      tenant.Id,
      new UpdateDeviceGroupRequestDto("Pre-production", "Pre-prod fleet"),
      TestContext.Current.CancellationToken);

    var dto = Assert.IsType<DeviceGroupDetailDto>(Assert.IsType<OkObjectResult>(result.Result!).Value);
    Assert.Equal("Pre-production", dto.Name);
    Assert.Equal("Pre-prod fleet", dto.Description);
  }

  private static async Task<Guid> CreateGroupAsync(DeviceGroupsController controller, Guid tenantId, string name)
  {
    var result = await controller.Create(
      tenantId,
      new CreateDeviceGroupRequestDto(name, null),
      TestContext.Current.CancellationToken);
    var createdAt = Assert.IsType<CreatedAtActionResult>(result.Result);
    var dto = Assert.IsType<DeviceGroupDetailDto>(createdAt.Value);
    return dto.Id;
  }

  private static async Task ReplaceGroupAssignment(
    IServiceProvider services,
    Guid userId,
    Guid tenantId,
    string permissionName,
    PermissionScopeKind scopeKind,
    Guid scopeId)
  {
    using var scope = services.CreateScope();
    await using var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    var existingAssignments = await db.PermissionAssignments
      .Where(x => x.PrincipalKind == PermissionPrincipalKind.User &&
                  x.PrincipalId == userId &&
                  x.PermissionName == permissionName)
      .ToListAsync(TestContext.Current.CancellationToken);
    db.PermissionAssignments.RemoveRange(existingAssignments);
    db.PermissionAssignments.Add(PermissionAssignment.CreateGrant(
      PermissionPrincipalKind.User,
      userId,
      permissionName,
      scopeKind,
      scopeId,
      tenantId,
      new PrincipalDescriptor(PrincipalType.User, userId, tenantId, "test")));
    await db.SaveChangesAsync(TestContext.Current.CancellationToken);
  }
}
