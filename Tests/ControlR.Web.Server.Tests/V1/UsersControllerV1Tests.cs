using ControlR.Web.Server.Api.V1;
using ControlR.Web.Server.Data;
using ControlR.Web.Server.Services.Authorization;
using ControlR.Web.Server.Services.Users;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.Users;

namespace ControlR.Web.Server.Tests.V1;

public class UsersControllerV1Tests(ITestOutputHelper testOutput)
{
  private readonly ITestOutputHelper _testOutput = testOutput;

  [Fact]
  public async Task Create_WhenServerPrincipalTargetsExistingTenant_ReturnsCreated()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.Services.CreateTestTenant("Destination Tenant");

    using var scope = testApp.CreateScope();
    var controller = scope.CreateController<UsersController>();
    controller.ControllerContext.HttpContext.User = await testApp.Services.CreateServerPrincipal();

    var result = await controller.Create(
      scope.ServiceProvider.GetRequiredService<AppDb>(),
      scope.ServiceProvider.GetRequiredService<IPermissionEvaluator>(),
      scope.ServiceProvider.GetRequiredService<IUserCreator>(),
      tenant.Id,
      new CreateUserRequestDto("cross-tenant", "cross-tenant@t.local", "T3stP@ssw0rd!", null),
      TestContext.Current.CancellationToken);

    var created = Assert.IsType<CreatedAtActionResult>(result.Result);
    Assert.IsType<UserResponseDto>(created.Value);
  }

  [Fact]
  public async Task Create_WhenTenantDoesNotExist_ReturnsBadRequest()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();

    // A server principal carries no tenant claim, so TryResolveTenantId trusts the parameter.
    // Without the tenant-existence check the create would write a user row owned by no tenant.
    var controller = scope.CreateController<UsersController>();
    controller.ControllerContext.HttpContext.User = await testApp.Services.CreateServerPrincipal();

    var result = await controller.Create(
      scope.ServiceProvider.GetRequiredService<AppDb>(),
      scope.ServiceProvider.GetRequiredService<IPermissionEvaluator>(),
      scope.ServiceProvider.GetRequiredService<IUserCreator>(),
      Guid.NewGuid(),
      new CreateUserRequestDto("ghost", "ghost@t.local", "T3stP@ssw0rd!", null),
      TestContext.Current.CancellationToken);

    Assert.IsType<BadRequestObjectResult>(result.Result);
  }
}
