using ControlR.Libraries.Shared.Dtos.ServerApi;
using ControlR.Web.Client.Authz;
using ControlR.Web.Server.Api;
using ControlR.Web.Server.Data;
using ControlR.Web.Server.Data.Entities;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace ControlR.Web.Server.Tests;

public class UsersControllerServerAdminTests(ITestOutputHelper testOutput)
{
  private readonly ITestOutputHelper _testOutputHelper = testOutput;

  [Fact]
  public async Task NonServerAdmin_CannotCreate_ServerAdministratorRole()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    var services = testApp.App.Services;

    // Create a tenant admin caller (not a server admin)
    var (controller, tenant, adminUser) = await services.CreateControllerWithTestData<UsersController>(
      roles: RoleNames.TenantAdministrator);

    using var db = services.GetRequiredService<AppDb>();

    var serverRole = await db.Roles.FirstAsync(r => r.Name == RoleNames.ServerAdministrator);

    var request = new CreateUserRequestDto(
      UserName: "evil",
      Email: "evil@t.local",
      Password: "P@ssw0rd!",
      RoleIds: [serverRole.Id],
      TagIds: null);

    var result = await controller.Create(
      services.GetRequiredService<AppDb>(),
      services.GetRequiredService<UserManager<AppUser>>(),
      services.GetRequiredService<Services.Users.IUserCreator>(),
      request);

    // Forbid translates to ForbidResult
    Assert.IsType<ForbidResult>(result.Result);
  }

  [Fact]
  public async Task ServerAdmin_CanCreate_ServerAdministratorRole()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    var services = testApp.App.Services;

    // Create a server admin caller
    var (controller, tenant, serverAdmin) = await services.CreateControllerWithTestData<UsersController>(
      roles: RoleNames.ServerAdministrator);

    using var db = services.GetRequiredService<AppDb>();

    // Ensure the ServerAdministrator role exists using RoleManager
    var serverRole = await db.Roles.FirstAsync(r => r.Name == RoleNames.ServerAdministrator);

    var request = new CreateUserRequestDto(
      UserName: "super",
      Email: "super@t.local",
      Password: "P@ssw0rd!",
      RoleIds: [serverRole.Id],
      TagIds: null);

    var result = await controller.Create(
      services.GetRequiredService<AppDb>(),
      services.GetRequiredService<UserManager<AppUser>>(),
      services.GetRequiredService<Services.Users.IUserCreator>(),
      request);

    Assert.IsType<CreatedAtActionResult>(result.Result);

    var createdUser = await db.Users.Include(u => u.UserRoles).FirstOrDefaultAsync(u => u.Email == "super@t.local");
    Assert.NotNull(createdUser);

    var userManager = services.GetRequiredService<UserManager<AppUser>>();
    var roles = await userManager.GetRolesAsync(createdUser!);
    Assert.Contains(RoleNames.ServerAdministrator, roles);
  }
}
