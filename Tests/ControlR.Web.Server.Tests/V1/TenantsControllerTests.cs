using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1;
using ControlR.Web.Server.Api.V1;
using ControlR.Web.Server.Data;
using ControlR.Web.Server.Data.Entities;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace ControlR.Web.Server.Tests.V1;

public class TenantsControllerTests(ITestOutputHelper testOutput)
{
  [Fact]
  public async Task Create_OnEmptyName_ReturnsBadRequest()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(testOutput);
    using var scope = testApp.CreateScope();

    var controller = await TestPrincipalHelper.CreateControllerWithServerServiceAccountAsync<
      TenantsController>(scope, cancellationToken: TestContext.Current.CancellationToken);

    var result = await controller.Create(
      new CreateTenantRequestDto(""),
      TestContext.Current.CancellationToken);

    var badRequest = Assert.IsType<ObjectResult>(result.Result);
    Assert.Equal(400, badRequest.StatusCode);
  }

  [Fact]
  public async Task Create_ReturnsCreatedAtActionResult()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(testOutput);
    using var scope = testApp.CreateScope();

    var controller = await TestPrincipalHelper.CreateControllerWithServerServiceAccountAsync<
      TenantsController>(scope, cancellationToken: TestContext.Current.CancellationToken);

    var result = await controller.Create(
      new CreateTenantRequestDto("New Test Tenant"),
      TestContext.Current.CancellationToken);

    var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
    var dto = Assert.IsType<CreateTenantResponseDto>(createdResult.Value);
    Assert.Equal("New Test Tenant", dto.TenantName);
    Assert.NotEqual(Guid.Empty, dto.TenantId);
  }

  [Fact]
  public async Task Get_ReturnsOk()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    await using var appDb = services.GetRequiredService<AppDb>();

    var tenant = new Tenant { Id = Guid.NewGuid(), Name = "Get Test Tenant" };
    appDb.Tenants.Add(tenant);
    await appDb.SaveChangesAsync(TestContext.Current.CancellationToken);

    var controller = await TestPrincipalHelper.CreateControllerWithServerServiceAccountAsync<
      TenantsController>(scope, cancellationToken: TestContext.Current.CancellationToken);

    var result = await controller.Get(tenant.Id, TestContext.Current.CancellationToken);
    var okResult = Assert.IsType<OkObjectResult>(result.Result);
    var dto = Assert.IsType<GetTenantResponseDto>(okResult.Value);
    Assert.Equal(tenant.Id, dto.TenantId);
    Assert.Equal("Get Test Tenant", dto.TenantName);
  }

  [Fact]
  public async Task Get_WhenNotFound_ReturnsNotFound()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(testOutput);
    using var scope = testApp.CreateScope();

    var controller = await TestPrincipalHelper.CreateControllerWithServerServiceAccountAsync<
      TenantsController>(scope, cancellationToken: TestContext.Current.CancellationToken);

    var result = await controller.Get(Guid.NewGuid(), TestContext.Current.CancellationToken);
    var notFound = Assert.IsType<ObjectResult>(result.Result);
    Assert.Equal(404, notFound.StatusCode);
  }
}