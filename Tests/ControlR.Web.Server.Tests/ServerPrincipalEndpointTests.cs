using System.Security.Claims;
using ControlR.Web.Client.Authz;
using ControlR.Web.Server.Api.V0;
using ControlR.Web.Server.Authn;
using ControlR.Web.Server.Data;
using ControlR.Web.Server.Data.Entities;
using ControlR.Web.Server.Services.LogonTokens;
using ControlR.Web.Server.Services.ServiceAccounts;
using ControlR.Web.Server.Tests.Helpers;
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
    var controller = scope.CreateController<V0InstallerKeysController>();
    controller.ControllerContext.HttpContext.User = await CreateServerPrincipal(services);

    var result = await controller.Create(new IssueInstallerKeyRequestDto(
      tenant.Id,
      creator.Id,
      InstallerKeyType.Persistent));

    Assert.NotNull(result.Result);
    var okResult = Assert.IsType<OkObjectResult>(result.Result);
    var response = Assert.IsType<CreateInstallerKeyResponseDto>(okResult.Value);
    Assert.NotEqual(Guid.Empty, response.Id);
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
    var controller = scope.CreateController<V0LogonTokensController>();
    controller.ControllerContext.HttpContext.User = await CreateServerPrincipal(services);
    controller.ControllerContext.HttpContext.Request.Scheme = "https";
    controller.ControllerContext.HttpContext.Request.Host = new HostString("localhost");

    var result = await controller.Create(
      services.GetRequiredService<AppDb>(),
      services.GetRequiredService<UserManager<AppUser>>(),
      services.GetRequiredService<ILogonTokenProvider>(),
      new IssueLogonTokenRequestDto(device.Id, tenant.Id, user.Id, null, LogonTokenKind.User));

    Assert.NotNull(result.Result);
    var okResult = Assert.IsType<OkObjectResult>(result.Result);
    var response = Assert.IsType<LogonTokenResponseDto>(okResult.Value);
    Assert.Contains($"deviceId={device.Id}", response.DeviceAccessUrl.Query, StringComparison.OrdinalIgnoreCase);
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
