using ControlR.Web.Server.Api.V1;
using ControlR.Web.Server.Data;
using ControlR.Web.Server.Data.Entities;
using ControlR.Web.Server.Services;
using ControlR.Web.Server.Services.Authorization;
using ControlR.Web.Server.Services.Users;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.PersonalAccessTokens;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.Users;

namespace ControlR.Web.Server.Tests.V1;

/// <summary>
/// User CRUD and the per-user PAT sub-resource on the V1 controller: required-tenantId
/// resolution, cross-tenant invisibility, the preset authority gates carried over from the
/// internal endpoint, and the temporary-password reset contract.
/// </summary>
public class UsersV1ControllerTests(ITestOutputHelper testOutput)
{
  private readonly ITestOutputHelper _testOutput = testOutput;

  [Fact]
  public async Task AdminResetPassword_ReturnsTemporaryPassword_AndRequiresPasswordChange()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var (controller, tenant, _) = await scope.CreateControllerWithTestData<UsersController>(
      userEmail: "v1-reset@test.local",
      presets: PermissionPresets.TenantAdministrator);
    var targetUser = await services.CreateTestUser(tenant.Id, "v1-reset-target@t.local");

    var result = await controller.AdminResetPassword(
      services.GetRequiredService<IPasswordManager>(),
      targetUser.Id,
      tenant.Id);

    var okResult = Assert.IsType<OkObjectResult>(result.Result);
    var dto = Assert.IsType<AdminResetPasswordResponseDto>(okResult.Value);

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
  public async Task AdminResetPassword_WhenTargetInOtherTenant_ReturnsNotFound()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var (controller, tenantA, _) = await scope.CreateControllerWithTestData<UsersController>(
      userEmail: "v1-reset-bad@test.local",
      presets: PermissionPresets.TenantAdministrator);

    // Address the caller's own tenant so the tenant check passes and the manager's own
    // cross-tenant "User not found." refusal is what the test observes.
    var foreignTenant = await services.CreateTestTenant("V1 Reset Foreign");
    var foreignUser = await services.CreateTestUser(foreignTenant.Id, "v1-foreign@t.local");

    var result = await controller.AdminResetPassword(
      services.GetRequiredService<IPasswordManager>(),
      foreignUser.Id,
      tenantA.Id);

    Assert.IsType<NotFoundResult>(result.Result);
  }

  [Fact]
  public async Task CreateUserPersonalAccessToken_WhenTargetIsOutsideTenant_ReturnsNotFound()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var (controller, tenantA, _) = await scope.CreateControllerWithTestData<UsersController>(
      userEmail: "v1-pat-other@test.local",
      presets: PermissionPresets.TenantAdministrator);

    var otherTenant = await services.CreateTestTenant("V1 PAT Other");
    var otherUser = await services.CreateTestUser(otherTenant.Id, "v1-pat-other-user@t.local");

    var result = await controller.CreateUserPersonalAccessToken(
      services.GetRequiredService<IPersonalAccessTokenManager>(),
      services.GetRequiredService<AppDb>(),
      otherUser.Id,
      tenantA.Id,
      new CreatePersonalAccessTokenRequestDto("Should Fail", PersonalAccessTokenPermissionMode.InheritOwner),
      TestContext.Current.CancellationToken);

    Assert.IsType<NotFoundResult>(result.Result);
  }

  [Fact]
  public async Task Create_WhenCallerRequestsAnotherTenant_Forbids()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var (controller, _, _) = await scope.CreateControllerWithTestData<UsersController>(
      userEmail: "v1-create-forbid@test.local",
      presets: PermissionPresets.TenantAdministrator);

    var foreignTenant = await services.CreateTestTenant("V1 Create Foreign");

    var result = await controller.Create(
      services.GetRequiredService<AppDb>(),
      services.GetRequiredService<IPermissionEvaluator>(),
      services.GetRequiredService<IUserCreator>(),
      foreignTenant.Id,
      new CreateUserRequestDto("stray", "stray@t.local", "P@ssw0rd!", null),
      TestContext.Current.CancellationToken);

    Assert.IsType<ForbidResult>(result.Result);
  }

  [Fact]
  public async Task Create_WhenPresetIsUnknown_ReturnsBadRequest()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var (controller, tenant, _) = await scope.CreateControllerWithTestData<UsersController>(
      userEmail: "v1-create-badpreset@test.local",
      presets: PermissionPresets.TenantAdministrator);

    var result = await controller.Create(
      services.GetRequiredService<AppDb>(),
      services.GetRequiredService<IPermissionEvaluator>(),
      services.GetRequiredService<IUserCreator>(),
      tenant.Id,
      new CreateUserRequestDto("nouser", "nouser@t.local", "P@ssw0rd!", ["Nonexistent Preset"]),
      TestContext.Current.CancellationToken);

    Assert.IsType<BadRequestObjectResult>(result.Result);
  }

  [Fact]
  public async Task Create_WithPresets_GrantsSeededPermissions_AndReturnsCreated()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var (controller, tenant, _) = await scope.CreateControllerWithTestData<UsersController>(
      userEmail: "v1-create@test.local",
      presets: PermissionPresets.TenantAdministrator);

    var result = await controller.Create(
      services.GetRequiredService<AppDb>(),
      services.GetRequiredService<IPermissionEvaluator>(),
      services.GetRequiredService<IUserCreator>(),
      tenant.Id,
      new CreateUserRequestDto("newuser", "newuser@t.local", "P@ssw0rd!", [PermissionPresets.DeviceSuperUser]),
      TestContext.Current.CancellationToken);

    var created = Assert.IsType<CreatedAtActionResult>(result.Result);
    var dto = Assert.IsType<UserResponseDto>(created.Value);
    Assert.Equal("newuser@t.local", dto.Email);

    await using var db = services.GetRequiredService<AppDb>();
    var hasDeviceRead = await db.PermissionAssignments.AnyAsync(
      x => x.PrincipalId == dto.Id && x.PermissionName == PermissionNames.DeviceRead,
      TestContext.Current.CancellationToken);
    Assert.True(hasDeviceRead);
  }

  [Fact]
  public async Task Delete_WhenCallerIsSelf_ReturnsBadRequest()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var (controller, tenant, user) = await scope.CreateControllerWithTestData<UsersController>(
      userEmail: "v1-selfdelete@test.local",
      presets: PermissionPresets.TenantAdministrator);

    var result = await controller.Delete(
      services.GetRequiredService<UserManager<AppUser>>(),
      services.GetRequiredService<AppDb>(),
      user.Id,
      tenant.Id,
      TestContext.Current.CancellationToken);

    Assert.IsType<BadRequestObjectResult>(result);
  }

  [Fact]
  public async Task Delete_WhenTargetInOtherTenant_ReturnsNotFound()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var (controller, tenantA, _) = await scope.CreateControllerWithTestData<UsersController>(
      userEmail: "v1-delete-a@test.local",
      presets: PermissionPresets.TenantAdministrator);

    var foreignTenant = await services.CreateTestTenant("V1 Delete Foreign");
    var foreignUser = await services.CreateTestUser(foreignTenant.Id, "v1-delete-foreign@t.local");

    var result = await controller.Delete(
      services.GetRequiredService<UserManager<AppUser>>(),
      services.GetRequiredService<AppDb>(),
      foreignUser.Id,
      tenantA.Id,
      TestContext.Current.CancellationToken);

    Assert.IsType<NotFoundResult>(result);
  }

  [Fact]
  public async Task Delete_WhenTargetIsValid_RemovesUser()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var (controller, tenant, _) = await scope.CreateControllerWithTestData<UsersController>(
      userEmail: "v1-delete@test.local",
      presets: PermissionPresets.TenantAdministrator);
    var targetUser = await services.CreateTestUser(tenant.Id, "v1-delete-target@t.local");

    var result = await controller.Delete(
      services.GetRequiredService<UserManager<AppUser>>(),
      services.GetRequiredService<AppDb>(),
      targetUser.Id,
      tenant.Id,
      TestContext.Current.CancellationToken);

    Assert.IsType<NoContentResult>(result);

    await using var appDb = services.GetRequiredService<AppDb>();
    var remaining = await appDb.Users.CountAsync(
      x => x.Id == targetUser.Id, TestContext.Current.CancellationToken);
    Assert.Equal(0, remaining);
  }

  [Fact]
  public async Task GetAll_ExcludesOtherTenantsUsers()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var (controller, tenantA, caller) = await scope.CreateControllerWithTestData<UsersController>(
      userEmail: "v1-all-a@test.local",
      presets: PermissionPresets.TenantAdministrator);

    await services.CreateTestUser(tenantA.Id, "v1-all-mine@t.local");
    var tenantB = await services.CreateTestTenant("V1 All B");
    var foreignUser = await services.CreateTestUser(tenantB.Id, "v1-all-theirs@t.local");

    var result = await controller.GetAll(
      services.GetRequiredService<AppDb>(),
      tenantA.Id,
      TestContext.Current.CancellationToken);

    var ok = Assert.IsType<OkObjectResult>(result.Result);
    var response = Assert.IsType<UsersResponseDto>(ok.Value);

    Assert.Contains(response.Items, x => x.Id == caller.Id);
    Assert.DoesNotContain(response.Items, x => x.Id == foreignUser.Id);
  }

  [Fact]
  public async Task PersonalAccessTokenCrud_ManagesTokens_ForTargetUserInTenant()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var (controller, tenant, _) = await scope.CreateControllerWithTestData<UsersController>(
      userEmail: "v1-pat-crud@test.local",
      presets: PermissionPresets.TenantAdministrator);
    var targetUser = await services.CreateTestUser(tenant.Id, "v1-pat-target@t.local");

    var createResult = await controller.CreateUserPersonalAccessToken(
      services.GetRequiredService<IPersonalAccessTokenManager>(),
      services.GetRequiredService<AppDb>(),
      targetUser.Id,
      tenant.Id,
      new CreatePersonalAccessTokenRequestDto("Admin Created PAT", PersonalAccessTokenPermissionMode.InheritOwner),
      TestContext.Current.CancellationToken);

    var created = Assert.IsType<CreatedAtActionResult>(createResult.Result);
    var createDto = Assert.IsType<CreatePersonalAccessTokenResponseDto>(created.Value);
    Assert.False(string.IsNullOrWhiteSpace(createDto.PlainTextToken));

    var getResult = await controller.GetUserPersonalAccessTokens(
      services.GetRequiredService<IPersonalAccessTokenManager>(),
      services.GetRequiredService<AppDb>(),
      targetUser.Id,
      tenant.Id,
      TestContext.Current.CancellationToken);

    var getOk = Assert.IsType<OkObjectResult>(getResult.Result);
    var tokens = Assert.IsAssignableFrom<IReadOnlyList<PersonalAccessTokenResponseDto>>(getOk.Value);
    var createdToken = Assert.Single(tokens);
    Assert.Equal(createDto.PersonalAccessToken.Id, createdToken.Id);

    var updateResult = await controller.UpdateUserPersonalAccessToken(
      services.GetRequiredService<IPersonalAccessTokenManager>(),
      services.GetRequiredService<AppDb>(),
      targetUser.Id,
      createdToken.Id,
      tenant.Id,
      new UpdatePersonalAccessTokenRequestDto("Renamed PAT"),
      TestContext.Current.CancellationToken);

    var updateOk = Assert.IsType<OkObjectResult>(updateResult.Result);
    var updatedToken = Assert.IsType<PersonalAccessTokenResponseDto>(updateOk.Value);
    Assert.Equal("Renamed PAT", updatedToken.Name);

    var deleteResult = await controller.DeleteUserPersonalAccessToken(
      services.GetRequiredService<IPersonalAccessTokenManager>(),
      services.GetRequiredService<AppDb>(),
      targetUser.Id,
      createdToken.Id,
      tenant.Id,
      TestContext.Current.CancellationToken);

    Assert.IsType<NoContentResult>(deleteResult);

    var finalGetResult = await controller.GetUserPersonalAccessTokens(
      services.GetRequiredService<IPersonalAccessTokenManager>(),
      services.GetRequiredService<AppDb>(),
      targetUser.Id,
      tenant.Id,
      TestContext.Current.CancellationToken);

    var finalGetOk = Assert.IsType<OkObjectResult>(finalGetResult.Result);
    var finalTokens = Assert.IsAssignableFrom<IReadOnlyList<PersonalAccessTokenResponseDto>>(finalGetOk.Value);
    Assert.Empty(finalTokens);
  }
}
