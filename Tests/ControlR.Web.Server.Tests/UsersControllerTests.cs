using ControlR.Libraries.Api.Contracts.Dtos.ServerApi;
using ControlR.Web.Client.Authz;
using ControlR.Web.Server.Api;
using ControlR.Web.Server.Data;
using ControlR.Web.Server.Data.Entities;
using ControlR.Web.Server.Services.Users;
using ControlR.Web.Server.Services;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ControlR.Web.Server.Tests;

public class UsersControllerTests(ITestOutputHelper testOutput)
{
  private readonly ITestOutputHelper _testOutputHelper = testOutput;

  [Fact]
  public async Task AdminResetPassword_ReturnsTemporaryPassword_AndRequiresPasswordChange()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);

    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var (controller, tenant, _) = await scope.CreateControllerWithTestData<UsersController>(
      roles: RoleNames.TenantAdministrator);
    var targetUser = await services.CreateTestUser(tenant.Id, "target@t.local");
    var userManager = services.GetRequiredService<UserManager<AppUser>>();

    var result = await controller.AdminResetPassword(
      targetUser.Id,
      services.GetRequiredService<IPasswordManager>());

    var okResult = Assert.IsType<OkObjectResult>(result.Result);
    var dto = Assert.IsType<AdminResetPasswordResponseDto>(okResult.Value);

    Assert.Equal(16, dto.TemporaryPassword.Length);

    using var verificationScope = testApp.CreateScope();
    var verificationUserManager = verificationScope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
    var refreshedUser = await verificationUserManager.FindByIdAsync(targetUser.Id.ToString());
    Assert.NotNull(refreshedUser);
    Assert.True(refreshedUser.RequirePasswordChange);
    Assert.True(await verificationUserManager.CheckPasswordAsync(refreshedUser, dto.TemporaryPassword));
  }

  [Fact]
  public async Task CreateUserPersonalAccessToken_ReturnsNotFound_ForUserOutsideTenant()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);

    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var (controller, _, _) = await scope.CreateControllerWithTestData<UsersController>(
      roles: RoleNames.TenantAdministrator);
    var otherTenant = await services.CreateTestTenant("Other Tenant");
    var otherUser = await services.CreateTestUser(otherTenant.Id, "other@t.local");

    var result = await controller.CreateUserPersonalAccessToken(
      otherUser.Id,
      services.GetRequiredService<IPersonalAccessTokenManager>(),
      services.GetRequiredService<AppDb>(),
      new CreatePersonalAccessTokenRequestDto("Should Fail"));

    Assert.IsType<NotFoundResult>(result.Result);
  }

  [Fact]
  public async Task Create_CreatesUser_WithRolesAndTags()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);

    // Arrange: create tenant, admin user and test role/tag
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var (controller, tenant, _) = await scope.CreateControllerWithTestData<UsersController>(
      "Tenant1",
      "admin@t.local",
      RoleNames.TenantAdministrator);

    await using var db = scope.ServiceProvider.GetRequiredService<AppDb>();

    // create an extra role to assign using RoleManager to ensure Identity data is consistent
    var roleId = Guid.NewGuid();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<AppRole>>();
    var customRole = new AppRole { Id = roleId, Name = "CustomRole" };
    await roleManager.CreateAsync(customRole);

    // create a tag to assign
    var tagId = Guid.NewGuid();
    db.Tags.Add(new Tag { Id = tagId, Name = "Tag1", TenantId = tenant.Id });
    await db.SaveChangesAsync(TestContext.Current.CancellationToken);

    var request = new CreateUserRequestDto(
      "newuser",
      "newuser@t.local",
      "P@ssw0rd!",
      [roleId],
      [tagId]);

    // Act
    var result = await controller.Create(
      services.GetRequiredService<AppDb>(),
      services.GetRequiredService<UserManager<AppUser>>(),
      services.GetRequiredService<IUserCreator>(),
      request);

    // Assert
    Assert.IsType<CreatedAtActionResult>(result.Result);

    // Verify user exists and has role and tag
    var createdUser = await db.Users
      .Include(appUser => appUser.Tags)
      .Include(u => u.UserRoles)
      .Include(u => u.Tags)
      .FirstOrDefaultAsync(u => u.Email == "newuser@t.local", TestContext.Current.CancellationToken);
    
    Assert.NotNull(createdUser);
    Assert.NotNull(createdUser.Tags);
    Assert.Contains(createdUser.Tags, t => t.Id == tagId);
    var userManager = services.GetRequiredService<UserManager<AppUser>>();
    var roles = await userManager.GetRolesAsync(createdUser);
    Assert.Contains("CustomRole", roles);
  }

  [Fact]
  public async Task Create_ReturnsBadRequest_WhenRoleMissing()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;

    var (controller, _, _) = await scope.CreateControllerWithTestData<UsersController>(
      roles: RoleNames.TenantAdministrator);

    var missingRoleId = Guid.NewGuid();

    var request = new CreateUserRequestDto(
      "nouser",
      "nouser@t.local",
      "P@ssw0rd!",
      [missingRoleId],
      null);

    var result = await controller.Create(
      services.GetRequiredService<AppDb>(),
      services.GetRequiredService<UserManager<AppUser>>(),
      services.GetRequiredService<IUserCreator>(),
      request);

    Assert.IsType<BadRequestObjectResult>(result.Result);
  }

  [Fact]
  public async Task Create_ReturnsBadRequest_WhenTagMissing()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;

    var (controller, _, _) = await scope.CreateControllerWithTestData<UsersController>(
      roles: RoleNames.TenantAdministrator);

    var missingTagId = Guid.NewGuid();

    var request = new CreateUserRequestDto(
      "nouser",
      "nouser@t.local",
      "P@ssw0rd!",
      null,
      [missingTagId]);

    var result = await controller.Create(
      services.GetRequiredService<AppDb>(),
      services.GetRequiredService<UserManager<AppUser>>(),
      services.GetRequiredService<IUserCreator>(),
      request);

    Assert.IsType<BadRequestObjectResult>(result.Result);
  }

  [Fact]
  public async Task PersonalAccessTokenCrud_ManagesTokens_ForTargetUserInTenant()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);

    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var (controller, tenant, _) = await scope.CreateControllerWithTestData<UsersController>(
      roles: RoleNames.TenantAdministrator);
    var targetUser = await services.CreateTestUser(tenant.Id, "pat-target@t.local");

    var createResult = await controller.CreateUserPersonalAccessToken(
      targetUser.Id,
      services.GetRequiredService<IPersonalAccessTokenManager>(),
      services.GetRequiredService<AppDb>(),
      new CreatePersonalAccessTokenRequestDto("Admin Created PAT"));

    var createOk = Assert.IsType<OkObjectResult>(createResult.Result);
    var createDto = Assert.IsType<CreatePersonalAccessTokenResponseDto>(createOk.Value);

    var getResult = await controller.GetUserPersonalAccessTokens(
      targetUser.Id,
      services.GetRequiredService<IPersonalAccessTokenManager>(),
      services.GetRequiredService<AppDb>());

    var getOk = Assert.IsType<OkObjectResult>(getResult.Result);
    var tokens = Assert.IsAssignableFrom<IEnumerable<PersonalAccessTokenDto>>(getOk.Value);
    var createdToken = Assert.Single(tokens);
    Assert.Equal(createDto.PersonalAccessToken.Id, createdToken.Id);

    var updateResult = await controller.UpdateUserPersonalAccessToken(
      targetUser.Id,
      createdToken.Id,
      services.GetRequiredService<IPersonalAccessTokenManager>(),
      services.GetRequiredService<AppDb>(),
      new UpdatePersonalAccessTokenRequestDto("Renamed PAT"));

    var updateOk = Assert.IsType<OkObjectResult>(updateResult.Result);
    var updatedToken = Assert.IsType<PersonalAccessTokenDto>(updateOk.Value);
    Assert.Equal("Renamed PAT", updatedToken.Name);

    var deleteResult = await controller.DeleteUserPersonalAccessToken(
      targetUser.Id,
      createdToken.Id,
      services.GetRequiredService<IPersonalAccessTokenManager>(),
      services.GetRequiredService<AppDb>());

    Assert.IsType<NoContentResult>(deleteResult);

    var finalGetResult = await controller.GetUserPersonalAccessTokens(
      targetUser.Id,
      services.GetRequiredService<IPersonalAccessTokenManager>(),
      services.GetRequiredService<AppDb>());

    var finalGetOk = Assert.IsType<OkObjectResult>(finalGetResult.Result);
    var finalTokens = Assert.IsAssignableFrom<IEnumerable<PersonalAccessTokenDto>>(finalGetOk.Value);
    Assert.Empty(finalTokens);
  }
}