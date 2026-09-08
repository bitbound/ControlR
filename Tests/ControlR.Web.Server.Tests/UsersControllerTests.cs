using ControlR.Web.Server.Api.Internal;
using ControlR.Web.Server.Data;
using ControlR.Web.Server.Data.Entities;
using ControlR.Web.Server.Services.Authorization;
using ControlR.Web.Server.Services.Users;
using ControlR.Web.Server.Services;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

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
      presets: PermissionPresets.TenantAdministrator);
    var targetUser = await services.CreateTestUser(tenant.Id, "target@t.local");
    var userManager = services.GetRequiredService<UserManager<AppUser>>();

    var result = await controller.AdminResetPassword(
      targetUser.Id,
      services.GetRequiredService<IPasswordManager>());

    var okResult = Assert.IsType<OkObjectResult>(result.Result);
    var dto = Assert.IsType<InternalDtos.AdminResetPasswordResponseDto>(okResult.Value);

    var identityOptions = services.GetRequiredService<IOptions<IdentityOptions>>();
    Assert.Equal(identityOptions.Value.Password.RequiredLength, dto.TemporaryPassword.Length);

    using var verificationScope = testApp.CreateScope();
    var verificationUserManager = verificationScope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
    var refreshedUser = await verificationUserManager.FindByIdAsync(targetUser.Id.ToString());
    Assert.NotNull(refreshedUser);
    Assert.True(refreshedUser.RequirePasswordChange);
    Assert.True(await verificationUserManager.CheckPasswordAsync(refreshedUser, dto.TemporaryPassword));
  }

  [Fact]
  public async Task AdminResetPassword_UsesMinimumLengthRequiredByEnabledCharacterTypes()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);

    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var identityOptions = services.GetRequiredService<IOptions<IdentityOptions>>().Value;
    identityOptions.Password.RequiredLength = 2;
    identityOptions.Password.RequireUppercase = true;
    identityOptions.Password.RequireLowercase = true;
    identityOptions.Password.RequireDigit = true;
    identityOptions.Password.RequireNonAlphanumeric = true;

    var (controller, tenant, _) = await scope.CreateControllerWithTestData<UsersController>(
      presets: PermissionPresets.TenantAdministrator);
    var targetUser = await services.CreateTestUser(tenant.Id, "short-policy@t.local");

    var result = await controller.AdminResetPassword(
      targetUser.Id,
      services.GetRequiredService<IPasswordManager>());

    var okResult = Assert.IsType<OkObjectResult>(result.Result);
    var dto = Assert.IsType<InternalDtos.AdminResetPasswordResponseDto>(okResult.Value);

    Assert.True(dto.TemporaryPassword.Length >= 4);
  }

  [Fact]
  public async Task CreateUserPersonalAccessToken_ReturnsNotFound_ForUserOutsideTenant()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);

    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var (controller, _, _) = await scope.CreateControllerWithTestData<UsersController>(
      presets: PermissionPresets.TenantAdministrator);
    var otherTenant = await services.CreateTestTenant("Other Tenant");
    var otherUser = await services.CreateTestUser(otherTenant.Id, "other@t.local");

    var result = await controller.CreateUserPersonalAccessToken(
      otherUser.Id,
      services.GetRequiredService<IPersonalAccessTokenManager>(),
      services.GetRequiredService<AppDb>(),
      new InternalDtos.CreatePersonalAccessTokenRequestDto("Should Fail", PersonalAccessTokenPermissionMode.InheritOwner));

    Assert.IsType<NotFoundResult>(result.Result);
  }

  [Fact]
  public async Task Create_CreatesUser_WithPresets()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);

    // Arrange: create tenant and admin user.
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var (controller, _, _) = await scope.CreateControllerWithTestData<UsersController>(
      "Tenant1",
      "admin@t.local",
      PermissionPresets.TenantAdministrator);

    await using var db = scope.ServiceProvider.GetRequiredService<AppDb>();

    var request = new InternalDtos.CreateUserRequestDto(
      "newuser",
      "newuser@t.local",
      "P@ssw0rd!",
      [PermissionPresets.DeviceSuperUser]);

    // Act
    var result = await controller.Create(
      services.GetRequiredService<AppDb>(),
      services.GetRequiredService<IPermissionEvaluator>(),
      services.GetRequiredService<IUserCreator>(),
      request);

    // Assert
    Assert.IsType<CreatedAtActionResult>(result.Result);

    // Verify user exists and has the preset's permissions.
    var createdUser = await db.Users
      .FirstOrDefaultAsync(u => u.Email == "newuser@t.local", TestContext.Current.CancellationToken);

    Assert.NotNull(createdUser);

    var hasDeviceRead = await db.PermissionAssignments.AnyAsync(
      x => x.PrincipalId == createdUser.Id && x.PermissionName == PermissionNames.DeviceRead,
      TestContext.Current.CancellationToken);
    Assert.True(hasDeviceRead);
  }

  [Fact]
  public async Task Create_ReturnsBadRequest_WhenPresetMissing()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;

    var (controller, _, _) = await scope.CreateControllerWithTestData<UsersController>(
      presets: PermissionPresets.TenantAdministrator);

    var request = new InternalDtos.CreateUserRequestDto(
      "nouser",
      "nouser@t.local",
      "P@ssw0rd!",
      ["Nonexistent Preset"]);

    var result = await controller.Create(
      services.GetRequiredService<AppDb>(),
      services.GetRequiredService<IPermissionEvaluator>(),
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
      presets: PermissionPresets.TenantAdministrator);
    var targetUser = await services.CreateTestUser(tenant.Id, "pat-target@t.local");

    var createResult = await controller.CreateUserPersonalAccessToken(
      targetUser.Id,
      services.GetRequiredService<IPersonalAccessTokenManager>(),
      services.GetRequiredService<AppDb>(),
      new InternalDtos.CreatePersonalAccessTokenRequestDto("Admin Created PAT", PersonalAccessTokenPermissionMode.InheritOwner));

    var createOk = Assert.IsType<OkObjectResult>(createResult.Result);
    var createDto = Assert.IsType<InternalDtos.CreatePersonalAccessTokenResponseDto>(createOk.Value);

    var getResult = await controller.GetUserPersonalAccessTokens(
      targetUser.Id,
      services.GetRequiredService<IPersonalAccessTokenManager>(),
      services.GetRequiredService<AppDb>());

    var getOk = Assert.IsType<OkObjectResult>(getResult.Result);
    var tokens = Assert.IsAssignableFrom<IEnumerable<InternalDtos.PersonalAccessTokenResponseDto>>(getOk.Value);
    var createdToken = Assert.Single(tokens);
    Assert.Equal(createDto.PersonalAccessToken.Id, createdToken.Id);

    var updateResult = await controller.UpdateUserPersonalAccessToken(
      targetUser.Id,
      createdToken.Id,
      services.GetRequiredService<IPersonalAccessTokenManager>(),
      services.GetRequiredService<AppDb>(),
      new InternalDtos.UpdatePersonalAccessTokenRequestDto("Renamed PAT"));

    var updateOk = Assert.IsType<OkObjectResult>(updateResult.Result);
    var updatedToken = Assert.IsType<InternalDtos.PersonalAccessTokenResponseDto>(updateOk.Value);
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
    var finalTokens = Assert.IsAssignableFrom<IEnumerable<InternalDtos.PersonalAccessTokenResponseDto>>(finalGetOk.Value);
    Assert.Empty(finalTokens);
  }
}
