using ControlR.Web.Server.Api.V1;
using ControlR.Web.Server.Data.Entities;
using ControlR.Web.Server.Services;
using ControlR.Web.Server.Services.Authorization;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using InviteDtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.Invites;

namespace ControlR.Web.Server.Tests.V1;

/// <summary>
/// Tenant invitation CRUD on the V1 controller: required-tenantId resolution, 201/204
/// conventions, the Items envelope, tenant isolation, and the TenantUsersWrite gate that
/// controls whether the returned invite URL carries the activation code.
/// </summary>
public class InvitesV1ControllerTests(ITestOutputHelper testOutput)
{
  private const string InviteConfirmationBasePath = "/invite-confirmation";

  private readonly ITestOutputHelper _testOutput = testOutput;

  [Fact]
  public async Task Create_ReturnsCreatedWithActivationCodeUrl()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var (controller, tenant, _) = await scope.CreateControllerWithTestData<InvitesController>(
      userEmail: "invites-create@test.local",
      presets: PermissionPresets.TenantAdministrator);
    ConfigureOrigin(controller);

    var result = await controller.Create(
      services.GetRequiredService<ITenantInvitesProvider>(),
      tenant.Id,
      new InviteDtos.CreateInviteRequestDto("invitee@test.local"));

    var created = Assert.IsType<CreatedAtActionResult>(result.Result);
    var dto = Assert.IsType<InviteDtos.InviteResponseDto>(created.Value);
    Assert.Equal("invitee@test.local", dto.InviteeEmail);
    Assert.StartsWith($"{InviteConfirmationBasePath}/", dto.InviteUrl.AbsolutePath);
  }

  [Fact]
  public async Task Create_WhenCallerRequestsAnotherTenant_Forbids()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var (controller, _, _) = await scope.CreateControllerWithTestData<InvitesController>(
      userEmail: "invites-forbid@test.local",
      presets: PermissionPresets.TenantAdministrator);
    ConfigureOrigin(controller);

    var foreignTenant = await services.CreateTestTenant("Invites Foreign");

    var result = await controller.Create(
      services.GetRequiredService<ITenantInvitesProvider>(),
      foreignTenant.Id,
      new InviteDtos.CreateInviteRequestDto("stray@test.local"));

    Assert.IsType<ForbidResult>(result.Result);
  }

  [Fact]
  public async Task Create_WhenInviteeAlreadyInvited_ReturnsConflict()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var (controller, tenant, _) = await scope.CreateControllerWithTestData<InvitesController>(
      userEmail: "invites-dup@test.local",
      presets: PermissionPresets.TenantAdministrator);
    ConfigureOrigin(controller);

    var provider = services.GetRequiredService<ITenantInvitesProvider>();
    var first = await controller.Create(
      provider,
      tenant.Id,
      new InviteDtos.CreateInviteRequestDto("dup@test.local"));
    Assert.IsType<CreatedAtActionResult>(first.Result);

    var second = await controller.Create(
      provider,
      tenant.Id,
      new InviteDtos.CreateInviteRequestDto("dup@test.local"));

    var problem = Assert.IsType<ObjectResult>(second.Result);
    Assert.Equal(StatusCodes.Status409Conflict, problem.StatusCode);
  }

  [Fact]
  public async Task Delete_RemovesInvite()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var (controller, tenant, _) = await scope.CreateControllerWithTestData<InvitesController>(
      userEmail: "invites-delete@test.local",
      presets: PermissionPresets.TenantAdministrator);
    ConfigureOrigin(controller);

    var provider = services.GetRequiredService<ITenantInvitesProvider>();
    var createResult = await controller.Create(
      provider,
      tenant.Id,
      new InviteDtos.CreateInviteRequestDto("doomed@test.local"));
    var created = Assert.IsType<CreatedAtActionResult>(createResult.Result);
    var dto = Assert.IsType<InviteDtos.InviteResponseDto>(created.Value);

    var deleteResult = await controller.Delete(provider, dto.Id, tenant.Id);
    Assert.IsType<NoContentResult>(deleteResult);

    var getResult = await controller.GetAll(
      provider,
      services.GetRequiredService<IPermissionEvaluator>(),
      services.GetRequiredService<IResourceDescriptorFactory>(),
      tenant.Id);
    var ok = Assert.IsType<OkObjectResult>(getResult.Result);
    var response = Assert.IsType<InviteDtos.InvitesResponseDto>(ok.Value);
    Assert.DoesNotContain(response.Items, x => x.Id == dto.Id);
  }

  [Fact]
  public async Task Delete_WhenInviteUnknown_ReturnsNotFoundProblem()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var (controller, tenant, _) = await scope.CreateControllerWithTestData<InvitesController>(
      userEmail: "invites-unknowndelete@test.local",
      presets: PermissionPresets.TenantAdministrator);
    ConfigureOrigin(controller);

    var result = await controller.Delete(
      services.GetRequiredService<ITenantInvitesProvider>(),
      Guid.NewGuid(),
      tenant.Id);

    var problem = Assert.IsType<ObjectResult>(result);
    Assert.Equal(StatusCodes.Status404NotFound, problem.StatusCode);
  }

  [Fact]
  public async Task GetAll_ReturnsOnlyCallersTenantInvites()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var (controller, tenant, _) = await scope.CreateControllerWithTestData<InvitesController>(
      userEmail: "invites-isolation@test.local",
      presets: PermissionPresets.TenantAdministrator);
    ConfigureOrigin(controller);

    var provider = services.GetRequiredService<ITenantInvitesProvider>();
    await controller.Create(
      provider,
      tenant.Id,
      new InviteDtos.CreateInviteRequestDto("mine@test.local"));

    // Seed an invite in a foreign tenant directly. The V1 list must never surface it.
    var foreignTenant = await services.CreateTestTenant("Invites Isolation Foreign");
    var foreignOrigin = new Uri("https://foreign.example");
    var foreignInvite = await provider.CreateInvite(
      "theirs@test.local",
      foreignTenant.Id,
      foreignOrigin,
      TestContext.Current.CancellationToken);
    Assert.True(foreignInvite.IsSuccess);

    var getResult = await controller.GetAll(
      provider,
      services.GetRequiredService<IPermissionEvaluator>(),
      services.GetRequiredService<IResourceDescriptorFactory>(),
      tenant.Id);

    var ok = Assert.IsType<OkObjectResult>(getResult.Result);
    var response = Assert.IsType<InviteDtos.InvitesResponseDto>(ok.Value);

    Assert.Contains(response.Items, x => x.InviteeEmail == "mine@test.local");
    Assert.DoesNotContain(response.Items, x => x.InviteeEmail == "theirs@test.local");
  }

  [Fact]
  public async Task GetAll_WhenCallerLacksTenantUsersWrite_OmitsActivationCode()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var (controller, tenant, _) = await scope.CreateControllerWithTestData<InvitesController>(
      userEmail: "invites-codemgr@test.local",
      presets: PermissionPresets.TenantAdministrator);
    ConfigureOrigin(controller);

    var provider = services.GetRequiredService<ITenantInvitesProvider>();
    await controller.Create(
      provider,
      tenant.Id,
      new InviteDtos.CreateInviteRequestDto("coded@test.local"));

    // A device-scoped user has no users.read or tenant.users.write grants. The endpoint is
    // invoked directly here, so only the in-handler code gate applies.
    var readOnlyUser = await services.CreateTestUser(
      tenant.Id,
      "invites-readonly@t.local",
      PermissionPresets.DeviceSuperUser);
    var readOnlyController = await scope.CreateControllerWithUser<InvitesController>(readOnlyUser);
    ConfigureOrigin(readOnlyController);

    var getResult = await readOnlyController.GetAll(
      provider,
      services.GetRequiredService<IPermissionEvaluator>(),
      services.GetRequiredService<IResourceDescriptorFactory>(),
      tenant.Id);

    var ok = Assert.IsType<OkObjectResult>(getResult.Result);
    var response = Assert.IsType<InviteDtos.InvitesResponseDto>(ok.Value);
    var invite = response.Items.Single(x => x.InviteeEmail == "coded@test.local");

    Assert.Equal(InviteConfirmationBasePath, invite.InviteUrl.AbsolutePath);
  }

  private static void ConfigureOrigin(InvitesController controller)
  {
    controller.ControllerContext.HttpContext!.Request.Scheme = Uri.UriSchemeHttps;
    controller.ControllerContext.HttpContext.Request.Host = new HostString("localhost");
  }
}
