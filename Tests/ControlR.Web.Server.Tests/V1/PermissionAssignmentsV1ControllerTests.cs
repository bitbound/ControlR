using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.PermissionAssignments;
using ControlR.Web.Server.Api.V1;
using ControlR.Web.Server.Authz.Permissions;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ControlR.Web.Server.Tests.V1;

/// <summary>
/// Tenant resolution, failure-status pass-through, and lifecycle behavior of the V1
/// permission-assignments endpoints. Direct controller calls bypass endpoint policies
/// (authorization middleware), so these exercise the in-handler tenant resolution, the
/// manager's write-authority evaluation (which flows through ToV1Failure verbatim, keeping
/// Forbidden refusals distinguishable from NotFound), and the manager's own lifecycle guards.
/// </summary>
public class PermissionAssignmentsV1ControllerTests(ITestOutputHelper testOutput)
{
  private readonly ITestOutputHelper _testOutput = testOutput;

  [Fact]
  public async Task Create_ReturnsCreatedAtActionWithDto()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();

    var (controller, tenant, user) = await scope.CreateControllerWithTestData<PermissionAssignmentsController>(
      "Create Tenant",
      "creator@test.local",
      PermissionPresets.TenantAdministrator);

    var result = await controller.Create(
      tenant.Id,
      new CreatePermissionAssignmentRequestDto(
        PermissionPrincipalKind.User,
        user.Id,
        PermissionNames.DeviceRead,
        PermissionEffect.Allow,
        PermissionScopeKind.Tenant,
        tenant.Id,
        "Device access",
        IsEnabled: false),
      TestContext.Current.CancellationToken);

    var createdAt = Assert.IsType<CreatedAtActionResult>(result.Result);
    var dto = Assert.IsType<PermissionAssignmentDto>(createdAt.Value);
    Assert.Equal(PermissionNames.DeviceRead, dto.PermissionName);
    Assert.Equal(user.Id, dto.PrincipalId);
    Assert.False(dto.IsEnabled);
    Assert.NotEqual(Guid.Empty, dto.Id);
  }

  [Fact]
  public async Task Create_ServerScopeByTenantAdmin_ReturnsForbidden()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();

    var (controller, tenant, user) = await scope.CreateControllerWithTestData<PermissionAssignmentsController>(
      "Authority Tenant",
      "authority@test.local",
      PermissionPresets.TenantAdministrator);

    var result = await controller.Create(
      tenant.Id,
      new CreatePermissionAssignmentRequestDto(
        PermissionPrincipalKind.User,
        user.Id,
        PermissionNames.ServerPermissionsWrite,
        PermissionEffect.Allow,
        PermissionScopeKind.Server,
        null,
        null),
      TestContext.Current.CancellationToken);

    // Manager write-authority refusals pass through as 403 (not collapsed to 404): they are
    // caller-authorization feedback within the caller's own tenant, never existence signals.
    ActionResultAsserts.AssertHttpStatus(result.Result, StatusCodes.Status403Forbidden);
  }

  [Fact]
  public async Task Create_WhenCallerRequestsAnotherTenant_Forbids()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();

    var (controller, _, _) = await scope.CreateControllerWithTestData<PermissionAssignmentsController>(
      "Tenant A",
      "a@test.local");
    var tenantB = await scope.ServiceProvider.CreateTestTenant("Tenant B");

    var result = await controller.Create(
      tenantB.Id,
      new CreatePermissionAssignmentRequestDto(
        PermissionPrincipalKind.User,
        Guid.NewGuid(),
        PermissionNames.DeviceRead,
        PermissionEffect.Allow,
        PermissionScopeKind.Tenant,
        tenantB.Id,
        null),
      TestContext.Current.CancellationToken);

    Assert.IsType<ForbidResult>(result.Result);
  }

  [Fact]
  public async Task Delete_WhenCallerRequestsAnotherTenant_Forbids()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();

    var (controller, _, _) = await scope.CreateControllerWithTestData<PermissionAssignmentsController>(
      "Tenant A",
      "a@test.local");
    var tenantB = await scope.ServiceProvider.CreateTestTenant("Tenant B");

    var result = await controller.Delete(
      Guid.NewGuid(), tenantB.Id, TestContext.Current.CancellationToken);

    Assert.IsType<ForbidResult>(result);
  }

  [Fact]
  public async Task Delete_WithServerPrincipal_RemovesAnotherTenantsAssignment()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();

    var (ownerController, tenant, user) = await scope.CreateControllerWithTestData<PermissionAssignmentsController>(
      "Delete Tenant",
      "owner@test.local",
      PermissionPresets.TenantAdministrator);
    var assignmentId = await CreateDeviceReadAsync(ownerController, tenant.Id, user.Id);

    var serverController = await scope.CreateControllerWithServerPrincipal<PermissionAssignmentsController>();
    var deleteResult = await serverController.Delete(
      assignmentId, tenant.Id, TestContext.Current.CancellationToken);
    Assert.IsType<NoContentResult>(deleteResult);

    var getResult = await serverController.GetByPrincipal(
      tenant.Id, PermissionPrincipalKind.User, user.Id, TestContext.Current.CancellationToken);
    var response = Assert.IsType<PermissionAssignmentsResponseDto>(
      Assert.IsType<OkObjectResult>(getResult.Result!).Value);
    Assert.DoesNotContain(response.Items, x => x.Id == assignmentId);
  }

  [Fact]
  public async Task GetByPrincipal_ReturnsCreatedAssignments()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();

    var (controller, tenant, user) = await scope.CreateControllerWithTestData<PermissionAssignmentsController>(
      "List Tenant",
      "lister@test.local",
      PermissionPresets.TenantAdministrator);

    await CreateDeviceReadAsync(controller, tenant.Id, user.Id);
    await CreateDeviceOverviewReadAsync(controller, tenant.Id, user.Id);

    var result = await controller.GetByPrincipal(
      tenant.Id, PermissionPrincipalKind.User, user.Id, TestContext.Current.CancellationToken);

    var response = Assert.IsType<PermissionAssignmentsResponseDto>(
      Assert.IsType<OkObjectResult>(result.Result!).Value);
    Assert.Contains(response.Items, x => x.PermissionName == PermissionNames.DeviceRead);
    Assert.Contains(response.Items, x => x.PermissionName == PermissionNames.DeviceOverviewRead);
  }

  [Fact]
  public async Task GetByPrincipal_WhenCallerRequestsAnotherTenant_Forbids()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();

    var (controller, _, _) = await scope.CreateControllerWithTestData<PermissionAssignmentsController>(
      "Tenant A",
      "a@test.local");
    var tenantB = await scope.ServiceProvider.CreateTestTenant("Tenant B");

    var result = await controller.GetByPrincipal(
      tenantB.Id, PermissionPrincipalKind.User, Guid.NewGuid(), TestContext.Current.CancellationToken);

    Assert.IsType<ForbidResult>(result.Result);
  }

  [Fact]
  public async Task GetCatalog_ReturnsCatalogEntries()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();

    var (controller, tenant, _) = await scope.CreateControllerWithTestData<PermissionAssignmentsController>(
      "Catalog Tenant",
      "cataloger@test.local",
      PermissionPresets.TenantAdministrator);

    var result = await controller.GetCatalog(tenant.Id, TestContext.Current.CancellationToken);

    var response = Assert.IsType<PermissionCatalogResponseDto>(
      Assert.IsType<OkObjectResult>(result.Result!).Value);
    Assert.NotEmpty(response.Items);
    Assert.Contains(response.Items, x => x.Name == PermissionNames.DeviceRead);
  }

  [Fact]
  public async Task Update_TogglesIsEnabled_ReturnsUpdatedDto()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();

    var (controller, tenant, user) = await scope.CreateControllerWithTestData<PermissionAssignmentsController>(
      "Update Tenant",
      "updater@test.local",
      PermissionPresets.TenantAdministrator);
    var assignmentId = await CreateDeviceReadAsync(controller, tenant.Id, user.Id);

    var result = await controller.Update(
      assignmentId,
      tenant.Id,
      new UpdatePermissionAssignmentRequestDto(
        PermissionNames.DeviceRead,
        PermissionEffect.Allow,
        PermissionScopeKind.Tenant,
        tenant.Id,
        null,
        IsEnabled: false),
      TestContext.Current.CancellationToken);

    var dto = Assert.IsType<PermissionAssignmentDto>(Assert.IsType<OkObjectResult>(result.Result!).Value);
    Assert.False(dto.IsEnabled);
  }

  private static async Task CreateDeviceOverviewReadAsync(
    PermissionAssignmentsController controller, Guid tenantId, Guid principalId)
  {
    var result = await controller.Create(
      tenantId,
      new CreatePermissionAssignmentRequestDto(
        PermissionPrincipalKind.User,
        principalId,
        PermissionNames.DeviceOverviewRead,
        PermissionEffect.Allow,
        PermissionScopeKind.Tenant,
        tenantId,
        null),
      TestContext.Current.CancellationToken);
    Assert.IsType<CreatedAtActionResult>(result.Result);
  }

  private static async Task<Guid> CreateDeviceReadAsync(
    PermissionAssignmentsController controller, Guid tenantId, Guid principalId)
  {
    var result = await controller.Create(
      tenantId,
      new CreatePermissionAssignmentRequestDto(
        PermissionPrincipalKind.User,
        principalId,
        PermissionNames.DeviceRead,
        PermissionEffect.Allow,
        PermissionScopeKind.Tenant,
        tenantId,
        null),
      TestContext.Current.CancellationToken);
    var createdAt = Assert.IsType<CreatedAtActionResult>(result.Result);
    return Assert.IsType<PermissionAssignmentDto>(createdAt.Value).Id;
  }
}
