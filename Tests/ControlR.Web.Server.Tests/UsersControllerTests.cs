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
using System.Linq;
using Xunit.Abstractions;

namespace ControlR.Web.Server.Tests;

public class UsersControllerTests(ITestOutputHelper testOutput)
{
  private readonly ITestOutputHelper _testOutputHelper = testOutput;

  [Fact]
  public async Task Create_CreatesUser_WithRolesAndTags()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);

    // Arrange: create tenant, admin user and test role/tag
    var services = testApp.App.Services;
    var (controller, tenant, adminUser) = await services.CreateControllerWithTestData<UsersController>(
      tenantName: "Tenant1",
      userEmail: "admin@t.local",
      roles: RoleNames.TenantAdministrator);

    using var db = services.GetRequiredService<AppDb>();

  // create an extra role to assign using RoleManager to ensure Identity data is consistent
  var roleId = Guid.NewGuid();
  var roleManager = services.GetRequiredService<RoleManager<AppRole>>();
  var customRole = new AppRole { Id = roleId, Name = "CustomRole" };
  await roleManager.CreateAsync(customRole);

    // create a tag to assign
    var tagId = Guid.NewGuid();
    db.Tags.Add(new Tag { Id = tagId, Name = "Tag1", TenantId = tenant.Id });
    await db.SaveChangesAsync();

    var request = new CreateUserRequestDto(
      UserName: "newuser",
      Email: "newuser@t.local",
      Password: "P@ssw0rd!",
      RoleIds: new[] { roleId },
      TagIds: new[] { tagId });

    // Act
    var result = await controller.Create(
      services.GetRequiredService<AppDb>(),
      services.GetRequiredService<UserManager<AppUser>>(),
      services.GetRequiredService<Services.Users.IUserCreator>(),
      request);

    // Assert
    Assert.IsType<CreatedAtActionResult>(result.Result);

    // Verify user exists and has role and tag
  var createdUser = await db.Users.Include(u => u.UserRoles).Include(u => u.Tags).FirstOrDefaultAsync(u => u.Email == "newuser@t.local");
  Assert.NotNull(createdUser);
  Assert.NotNull(createdUser!.Tags);
  Assert.Contains(createdUser.Tags, t => t.Id == tagId);
    var userManager = services.GetRequiredService<UserManager<AppUser>>();
    var roles = await userManager.GetRolesAsync(createdUser);
    Assert.Contains("CustomRole", roles);
  }

  [Fact]
  public async Task Create_ReturnsBadRequest_WhenRoleMissing()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    var services = testApp.App.Services;

    var (controller, tenant, adminUser) = await services.CreateControllerWithTestData<UsersController>(
      roles: RoleNames.TenantAdministrator);

    var missingRoleId = Guid.NewGuid();

    var request = new CreateUserRequestDto(
      UserName: "nouser",
      Email: "nouser@t.local",
      Password: "P@ssw0rd!",
      RoleIds: new[] { missingRoleId },
      TagIds: null);

    var result = await controller.Create(
      services.GetRequiredService<AppDb>(),
      services.GetRequiredService<UserManager<AppUser>>(),
      services.GetRequiredService<Services.Users.IUserCreator>(),
      request);

    Assert.IsType<BadRequestObjectResult>(result.Result);
  }

  [Fact]
  public async Task Create_ReturnsBadRequest_WhenTagMissing()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    var services = testApp.App.Services;

    var (controller, tenant, adminUser) = await services.CreateControllerWithTestData<UsersController>(
      roles: RoleNames.TenantAdministrator);

    var missingTagId = Guid.NewGuid();

    var request = new CreateUserRequestDto(
      UserName: "nouser",
      Email: "nouser@t.local",
      Password: "P@ssw0rd!",
      RoleIds: null,
      TagIds: new[] { missingTagId });

    var result = await controller.Create(
      services.GetRequiredService<AppDb>(),
      services.GetRequiredService<UserManager<AppUser>>(),
      services.GetRequiredService<Services.Users.IUserCreator>(),
      request);

    Assert.IsType<BadRequestObjectResult>(result.Result);
  }
}
