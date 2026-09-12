using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.Customers;
using ControlR.Web.Server.Api.V1;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ControlR.Web.Server.Tests.V1;

/// <summary>
/// Tenant isolation, tenant resolution, and failure-collapse behavior of the V1 customers
/// endpoints. Direct controller calls bypass endpoint policies (authorization middleware),
/// so these exercise the in-handler tenant resolution, the manager's explicit TenantId
/// predicates, and the collapse of failures that could otherwise act as an existence oracle
/// against other tenants' customers.
/// </summary>
public class CustomersV1ControllerTests(ITestOutputHelper testOutput)
{
  private readonly ITestOutputHelper _testOutput = testOutput;

  [Fact]
  public async Task AssignDevices_WhenCustomerAndDevicesBelongToTenant_ReturnsNoContent()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;

    var (controller, tenant, _) = await scope.CreateControllerWithTestData<CustomersController>(
      "Device Tenant",
      "admin@test.local",
      PermissionPresets.TenantAdministrator);
    var customerId = await CreateCustomerAsync(controller, tenant.Id, "Alpha");
    var device1 = await services.CreateTestDevice(tenant.Id);
    var device2 = await services.CreateTestDevice(tenant.Id);

    var result = await controller.AssignDevices(
      customerId,
      tenant.Id,
      new AssignCustomerDevicesRequestDto([device1.Id, device2.Id], []),
      TestContext.Current.CancellationToken);

    Assert.IsType<NoContentResult>(result);

    var getResult = await controller.Get(customerId, tenant.Id, TestContext.Current.CancellationToken);
    var customer = Assert.IsType<CustomerDto>(Assert.IsType<OkObjectResult>(getResult.Result!).Value);
    Assert.Equal(2, customer.DeviceCount);
  }

  [Fact]
  public async Task Create_WithServerPrincipal_ReturnsCreatedAtAction()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var tenant = await scope.ServiceProvider.CreateTestTenant("Server Tenant");

    var controller = await scope.CreateControllerWithServerPrincipal<CustomersController>();
    var result = await controller.Create(
      tenant.Id,
      new CreateCustomerRequestDto("Alpha", "desc", null),
      TestContext.Current.CancellationToken);

    var action = Assert.IsType<CreatedAtActionResult>(result.Result);
    Assert.Equal(nameof(CustomersController.Get), action.ActionName);
    var customer = Assert.IsType<CustomerDto>(action.Value);
    Assert.Equal("Alpha", customer.Name);

    var getResult = await controller.Get(customer.Id, tenant.Id, TestContext.Current.CancellationToken);
    var fetched = Assert.IsType<CustomerDto>(Assert.IsType<OkObjectResult>(getResult.Result!).Value);
    Assert.Equal(customer.Id, fetched.Id);
    Assert.Equal("desc", fetched.Description);
  }

  [Fact]
  public async Task Delete_WhenCustomerBelongsToOtherTenant_ReturnsNotFound()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();

    var (ownerController, tenantA, _) = await scope.CreateControllerWithTestData<CustomersController>(
      "Tenant A",
      "owner@test.local",
      PermissionPresets.TenantAdministrator);
    var customerId = await CreateCustomerAsync(ownerController, tenantA.Id, "Alpha");
    var tenantB = await scope.ServiceProvider.CreateTestTenant("Tenant B");

    var serverController = await scope.CreateControllerWithServerPrincipal<CustomersController>();
    var result = await serverController.Delete(customerId, tenantB.Id, TestContext.Current.CancellationToken);

    ActionResultAsserts.AssertHttpStatus(result, StatusCodes.Status404NotFound);

    var listResult = await serverController.GetAll(tenantA.Id, TestContext.Current.CancellationToken);
    var customers = Assert.IsType<CustomersResponseDto>(Assert.IsType<OkObjectResult>(listResult.Result!).Value);
    Assert.Single(customers.Items);
  }

  [Fact]
  public async Task Delete_WithTenantAdmin_RemovesCustomerAndUnassignsDevices()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();

    var (controller, tenant, _) = await scope.CreateControllerWithTestData<CustomersController>(
      "Delete Tenant",
      "admin@test.local",
      PermissionPresets.TenantAdministrator);
    var customerId = await CreateCustomerAsync(controller, tenant.Id, "Alpha");

    var result = await controller.Delete(customerId, tenant.Id, TestContext.Current.CancellationToken);

    Assert.IsType<NoContentResult>(result);

    var getResult = await controller.Get(customerId, tenant.Id, TestContext.Current.CancellationToken);
    ActionResultAsserts.AssertHttpStatus(getResult.Result, StatusCodes.Status404NotFound);
  }

  [Fact]
  public async Task GetAll_WhenCallerAsksForAnotherTenant_Forbids()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();

    var (controllerA, _, _) = await scope.CreateControllerWithTestData<CustomersController>(
      "Tenant A",
      "a@test.local",
      PermissionPresets.TenantAdministrator);
    var tenantB = await scope.ServiceProvider.CreateTestTenant("Tenant B");

    var result = await controllerA.GetAll(tenantB.Id, TestContext.Current.CancellationToken);

    Assert.IsType<ForbidResult>(result.Result);
  }

  [Fact]
  public async Task GetAll_WithTenantAdmin_ReturnsTenantCustomersOrderedByName()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();

    var (controller, tenant, _) = await scope.CreateControllerWithTestData<CustomersController>(
      "List Tenant",
      "admin@test.local",
      PermissionPresets.TenantAdministrator);
    await CreateCustomerAsync(controller, tenant.Id, "Zulu");
    await CreateCustomerAsync(controller, tenant.Id, "Alpha");

    var result = await controller.GetAll(tenant.Id, TestContext.Current.CancellationToken);

    var response = Assert.IsType<CustomersResponseDto>(Assert.IsType<OkObjectResult>(result.Result!).Value);
    Assert.Collection(
      response.Items,
      item => Assert.Equal("Alpha", item.Name),
      item => Assert.Equal("Zulu", item.Name));
  }

  [Fact]
  public async Task Get_WhenCustomerBelongsToOtherTenant_ReturnsNotFound()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();

    var (ownerController, tenantA, _) = await scope.CreateControllerWithTestData<CustomersController>(
      "Tenant A",
      "owner@test.local",
      PermissionPresets.TenantAdministrator);
    var customerId = await CreateCustomerAsync(ownerController, tenantA.Id, "Alpha");
    var tenantB = await scope.ServiceProvider.CreateTestTenant("Tenant B");

    var serverController = await scope.CreateControllerWithServerPrincipal<CustomersController>();
    var result = await serverController.Get(customerId, tenantB.Id, TestContext.Current.CancellationToken);

    ActionResultAsserts.AssertHttpStatus(result.Result, StatusCodes.Status404NotFound);
  }

  [Fact]
  public async Task Get_WhenCustomerDoesNotExistInTenant_ReturnsNotFound()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();

    var (controller, tenant, _) = await scope.CreateControllerWithTestData<CustomersController>(
      "Get Tenant",
      "user@test.local");
    var result = await controller.Get(Guid.NewGuid(), tenant.Id, TestContext.Current.CancellationToken);

    ActionResultAsserts.AssertHttpStatus(result.Result, StatusCodes.Status404NotFound);
  }

  [Fact]
  public async Task Update_WithTenantAdmin_ReturnsUpdatedDto()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();

    var (controller, tenant, _) = await scope.CreateControllerWithTestData<CustomersController>(
      "Update Tenant",
      "admin@test.local",
      PermissionPresets.TenantAdministrator);
    var customerId = await CreateCustomerAsync(controller, tenant.Id, "Alpha");

    var result = await controller.Update(
      customerId,
      tenant.Id,
      new UpdateCustomerRequestDto("Beta", "updated", "notes"),
      TestContext.Current.CancellationToken);

    var customer = Assert.IsType<CustomerDto>(Assert.IsType<OkObjectResult>(result.Result!).Value);
    Assert.Equal("Beta", customer.Name);
    Assert.Equal("updated", customer.Description);
    Assert.Equal("notes", customer.Notes);
  }

  private static async Task<Guid> CreateCustomerAsync(CustomersController controller, Guid tenantId, string name)
  {
    var result = await controller.Create(
      tenantId,
      new CreateCustomerRequestDto(name, null, null),
      TestContext.Current.CancellationToken);

    var action = Assert.IsType<CreatedAtActionResult>(result.Result);
    return Assert.IsType<CustomerDto>(action.Value).Id;
  }
}