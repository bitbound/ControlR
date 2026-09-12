using ControlR.Web.Server.Api.V1;
using ControlR.Web.Server.Data.Entities;
using ControlR.Web.Server.Services;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.PersonalAccessTokens;

namespace ControlR.Web.Server.Tests.V1;

/// <summary>
/// Self-service personal access tokens on the V1 controller: the caller's own tokens only,
/// required-tenantId resolution, the 201/204 status conventions, and the Items envelope.
/// </summary>
public class PersonalAccessTokensV1ControllerTests(ITestOutputHelper testOutput)
{
  private readonly ITestOutputHelper _testOutput = testOutput;

  [Fact]
  public async Task Create_ReturnsCreatedWithPlainTextToken()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var (controller, tenant, _) = await scope.CreateControllerWithTestData<PersonalAccessTokensController>(
      userEmail: "pat-self-create@test.local",
      presets: PermissionPresets.TenantAdministrator);

    var result = await controller.Create(
      services.GetRequiredService<IPersonalAccessTokenManager>(),
      services.GetRequiredService<UserManager<AppUser>>(),
      tenant.Id,
      new CreatePersonalAccessTokenRequestDto("self-token", PersonalAccessTokenPermissionMode.InheritOwner));

    var created = Assert.IsType<CreatedAtActionResult>(result.Result);
    var dto = Assert.IsType<CreatePersonalAccessTokenResponseDto>(created.Value);
    Assert.Equal("self-token", dto.PersonalAccessToken.Name);
    Assert.False(string.IsNullOrWhiteSpace(dto.PlainTextToken));
  }

  [Fact]
  public async Task Create_WhenCallerRequestsAnotherTenant_Forbids()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var (controller, _, _) = await scope.CreateControllerWithTestData<PersonalAccessTokensController>(
      userEmail: "pat-self-forbid@test.local",
      presets: PermissionPresets.TenantAdministrator);

    var foreignTenant = await services.CreateTestTenant("PAT Self Foreign");

    var result = await controller.Create(
      services.GetRequiredService<IPersonalAccessTokenManager>(),
      services.GetRequiredService<UserManager<AppUser>>(),
      foreignTenant.Id,
      new CreatePersonalAccessTokenRequestDto("stray", PersonalAccessTokenPermissionMode.InheritOwner));

    Assert.IsType<ForbidResult>(result.Result);
  }

  [Fact]
  public async Task Delete_RemovesCallersToken()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var (controller, tenant, _) = await scope.CreateControllerWithTestData<PersonalAccessTokensController>(
      userEmail: "pat-self-delete@test.local",
      presets: PermissionPresets.TenantAdministrator);

    var createResult = await controller.Create(
      services.GetRequiredService<IPersonalAccessTokenManager>(),
      services.GetRequiredService<UserManager<AppUser>>(),
      tenant.Id,
      new CreatePersonalAccessTokenRequestDto("doomed", PersonalAccessTokenPermissionMode.InheritOwner));
    var created = Assert.IsType<CreatedAtActionResult>(createResult.Result);
    var dto = Assert.IsType<CreatePersonalAccessTokenResponseDto>(created.Value);

    var deleteResult = await controller.Delete(
      services.GetRequiredService<IPersonalAccessTokenManager>(),
      services.GetRequiredService<UserManager<AppUser>>(),
      dto.PersonalAccessToken.Id,
      tenant.Id);

    Assert.IsType<NoContentResult>(deleteResult);

    var getResult = await controller.GetAll(
      services.GetRequiredService<IPersonalAccessTokenManager>(),
      services.GetRequiredService<UserManager<AppUser>>(),
      tenant.Id);
    var ok = Assert.IsType<OkObjectResult>(getResult.Result);
    var response = Assert.IsType<PersonalAccessTokensResponseDto>(ok.Value);
    Assert.DoesNotContain(response.Items, x => x.Id == dto.PersonalAccessToken.Id);
  }

  [Fact]
  public async Task Delete_WhenTokenBelongsToAnotherUser_ReturnsBadRequest()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var (controller, tenant, _) = await scope.CreateControllerWithTestData<PersonalAccessTokensController>(
      userEmail: "pat-self-crossdelete@test.local",
      presets: PermissionPresets.TenantAdministrator);

    // Seed a token for a different user in the same tenant. Deleting it must fail because
    // the self-service surface only ever manages the caller's own tokens.
    var otherUser = await services.CreateTestUser(tenant.Id, "pat-other-owner@t.local");
    var manager = services.GetRequiredService<IPersonalAccessTokenManager>();
    var created = await manager.CreateTokenWithKey(
      Guid.NewGuid(),
      new string('x', 64),
      "other-token",
      otherUser.Id,
      PersonalAccessTokenPermissionMode.InheritOwner);
    Assert.True(created.IsSuccess);

    var result = await controller.Delete(
      manager,
      services.GetRequiredService<UserManager<AppUser>>(),
      created.Value.Id,
      tenant.Id);

    Assert.IsType<BadRequestObjectResult>(result);
  }

  [Fact]
  public async Task Delete_WhenTokenUnknown_ReturnsBadRequest()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var (controller, tenant, _) = await scope.CreateControllerWithTestData<PersonalAccessTokensController>(
      userEmail: "pat-self-unknowndelete@test.local",
      presets: PermissionPresets.TenantAdministrator);

    var result = await controller.Delete(
      services.GetRequiredService<IPersonalAccessTokenManager>(),
      services.GetRequiredService<UserManager<AppUser>>(),
      Guid.NewGuid(),
      tenant.Id);

    Assert.IsType<BadRequestObjectResult>(result);
  }

  [Fact]
  public async Task GetAll_ExcludesOtherUsersTokens_AndReturnsEnvelope()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var (controller, tenant, _) = await scope.CreateControllerWithTestData<PersonalAccessTokensController>(
      userEmail: "pat-self-all@test.local",
      presets: PermissionPresets.TenantAdministrator);

    var createResult = await controller.Create(
      services.GetRequiredService<IPersonalAccessTokenManager>(),
      services.GetRequiredService<UserManager<AppUser>>(),
      tenant.Id,
      new CreatePersonalAccessTokenRequestDto("mine", PersonalAccessTokenPermissionMode.InheritOwner));
    var created = Assert.IsType<CreatedAtActionResult>(createResult.Result);
    var mine = Assert.IsType<CreatePersonalAccessTokenResponseDto>(created.Value);

    var otherUser = await services.CreateTestUser(tenant.Id, "pat-self-other@t.local");
    var manager = services.GetRequiredService<IPersonalAccessTokenManager>();
    var otherToken = await manager.CreateTokenWithKey(
      Guid.NewGuid(),
      new string('y', 64),
      "theirs",
      otherUser.Id,
      PersonalAccessTokenPermissionMode.InheritOwner);
    Assert.True(otherToken.IsSuccess);

    var getResult = await controller.GetAll(
      manager,
      services.GetRequiredService<UserManager<AppUser>>(),
      tenant.Id);

    var ok = Assert.IsType<OkObjectResult>(getResult.Result);
    var response = Assert.IsType<PersonalAccessTokensResponseDto>(ok.Value);

    Assert.Contains(response.Items, x => x.Id == mine.PersonalAccessToken.Id);
    Assert.DoesNotContain(response.Items, x => x.Id == otherToken.Value.Id);
  }

  [Fact]
  public async Task Update_RenamesToken()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var (controller, tenant, _) = await scope.CreateControllerWithTestData<PersonalAccessTokensController>(
      userEmail: "pat-self-rename@test.local",
      presets: PermissionPresets.TenantAdministrator);

    var createResult = await controller.Create(
      services.GetRequiredService<IPersonalAccessTokenManager>(),
      services.GetRequiredService<UserManager<AppUser>>(),
      tenant.Id,
      new CreatePersonalAccessTokenRequestDto("before", PersonalAccessTokenPermissionMode.InheritOwner));
    var created = Assert.IsType<CreatedAtActionResult>(createResult.Result);
    var dto = Assert.IsType<CreatePersonalAccessTokenResponseDto>(created.Value);

    var updateResult = await controller.Update(
      services.GetRequiredService<IPersonalAccessTokenManager>(),
      services.GetRequiredService<UserManager<AppUser>>(),
      dto.PersonalAccessToken.Id,
      tenant.Id,
      new UpdatePersonalAccessTokenRequestDto("after"));

    var ok = Assert.IsType<OkObjectResult>(updateResult.Result);
    var updated = Assert.IsType<PersonalAccessTokenResponseDto>(ok.Value);
    Assert.Equal("after", updated.Name);
  }
}
