using System.Security.Claims;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi;
using ControlR.Web.Client.Authz;
using ControlR.Web.Server.Api;
using ControlR.Web.Server.Authn;
using ControlR.Web.Server.Data;
using ControlR.Web.Server.Data.Entities;
using ControlR.Web.Server.Services.LogonTokens;
using ControlR.Web.Server.Services.ServiceAccounts;
using ControlR.Web.Server.Services;
using ControlR.Web.Server.Services.Users;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace ControlR.Web.Server.Tests;

public class ServerPrincipalEndpointTests(ITestOutputHelper testOutput)
{
  private readonly ITestOutputHelper _testOutput = testOutput;

  [Fact]
  public async Task ServerPrincipalCanCreateInstallerKeyWithExplicitContext()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;

    var tenant = await services.CreateTestTenant();
    var creator = await services.CreateTestUser(tenant.Id, email: "creator@test.local");
    var controller = scope.CreateController<InstallerKeysController>();
    controller.ControllerContext.HttpContext.User = await CreateServerPrincipal(services);

    var result = await controller.Create(new CreateInstallerKeyRequestDto(
      InstallerKeyType.Persistent,
      TenantId: tenant.Id,
      CreatorId: creator.Id));

    Assert.NotNull(result.Result);
    var okResult = Assert.IsType<OkObjectResult>(result.Result);
    var response = Assert.IsType<CreateInstallerKeyResponseDto>(okResult.Value);
    Assert.NotEqual(Guid.Empty, response.Id);
  }

  [Fact]
  public async Task ServerPrincipalCanCreateInviteWithExplicitTenant()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;

    var tenant = await services.CreateTestTenant();
    var controller = scope.CreateController<InvitesController>();
    controller.ControllerContext.HttpContext.User = await CreateServerPrincipal(services);
    controller.ControllerContext.HttpContext.Request.Scheme = "https";
    controller.ControllerContext.HttpContext.Request.Host = new HostString("localhost");

    var result = await controller.Create(
      new TenantInviteRequestDto("invitee@test.local", tenant.Id),
      services.GetRequiredService<ITenantInvitesProvider>());

    Assert.NotNull(result.Result);
    var okResult = Assert.IsType<OkObjectResult>(result.Result);
    var invite = Assert.IsType<TenantInviteResponseDto>(okResult.Value);
    Assert.Equal("invitee@test.local", invite.InviteeEmail);
  }

  [Fact]
  public async Task ServerPrincipalCanCreateLogonTokenWithExplicitUserAndTenant()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;

    var tenant = await services.CreateTestTenant();
    var user = await services.CreateTestUser(tenant.Id, email: "viewer@test.local");
    var device = await services.CreateTestDevice(tenant.Id);
    var controller = scope.CreateController<LogonTokenController>();
    controller.ControllerContext.HttpContext.User = await CreateServerPrincipal(services);
    controller.ControllerContext.HttpContext.Request.Scheme = "https";
    controller.ControllerContext.HttpContext.Request.Host = new HostString("localhost");

    var result = await controller.IssueLogonToken(
      services.GetRequiredService<AppDb>(),
      services.GetRequiredService<ILogonTokenProvider>(),
      new IssueLogonTokenRequestDto(device.Id, tenant.Id, user.Id));

    Assert.NotNull(result.Result);
    var okResult = Assert.IsType<OkObjectResult>(result.Result);
    var response = Assert.IsType<LogonTokenResponseDto>(okResult.Value);
    Assert.Contains($"deviceId={device.Id}", response.DeviceAccessUrl.Query, StringComparison.OrdinalIgnoreCase);
  }

  [Fact]
  public async Task ServerPrincipalCanCreateUserWithExplicitTenant()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;

    var tenant = await services.CreateTestTenant();
    var controller = scope.CreateController<UsersController>();
    controller.ControllerContext.HttpContext.User = await CreateServerPrincipal(services);

    var result = await controller.Create(
      services.GetRequiredService<AppDb>(),
      services.GetRequiredService<UserManager<AppUser>>(),
      services.GetRequiredService<IUserCreator>(),
      new CreateUserRequestDto(
        "server-created@test.local",
        "server-created@test.local",
        "T3stP@ssw0rd!",
        null,
        null,
        tenant.Id));

    Assert.NotNull(result.Result);
    var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
    var user = Assert.IsType<UserResponseDto>(createdResult.Value);
    Assert.Equal("server-created@test.local", user.Email);
  }

  [Fact]
  public async Task ServerPrincipalCanSetTenantSettingsWithExplicitTenant()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;

    var tenant = await services.CreateTestTenant();
    var controller = scope.CreateController<TenantSettingsController>();
    controller.ControllerContext.HttpContext.User = await CreateServerPrincipal(services);

    var result = await controller.SetSetting(new TenantSettingRequestDto("General:Timezone", "UTC", tenant.Id));

    Assert.NotNull(result.Result);
    var okResult = Assert.IsType<OkObjectResult>(result.Result);
    var response = Assert.IsType<TenantSettingResponseDto>(okResult.Value);
    Assert.Equal("General:Timezone", response.Name);
  }

  private static async Task<ClaimsPrincipal> CreateServerPrincipal(IServiceProvider services)
  {
    var serviceAccountManager = services.GetRequiredService<IServiceAccountManager>();
    var saResult = await serviceAccountManager.CreateServer(
      $"server-principal-{Guid.NewGuid():N}",
      null,
      TestContext.Current.CancellationToken);

    Assert.True(saResult.IsSuccess);
    var serviceAccount = saResult.Value.ServiceAccount;
    var credential = serviceAccount.Credentials[0];

    return new ClaimsPrincipal(new ClaimsIdentity(
    [
      new Claim(PrincipalClaimTypes.PrincipalType, PrincipalClaimTypes.ServerServiceAccount),
      new Claim(PrincipalClaimTypes.PrincipalId, serviceAccount.Id.ToString()),
      new Claim(UserClaimTypes.AuthenticationMethod, PrincipalClaimTypes.ServiceAccountCredentialMethod),
      new Claim(PrincipalClaimTypes.CredentialId, credential.Id.ToString()),
    ], "TestAuth"));
  }
}