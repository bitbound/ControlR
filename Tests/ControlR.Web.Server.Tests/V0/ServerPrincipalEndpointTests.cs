using System.Security.Claims;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V0;
using ControlR.Web.Client.Authz;
using ControlR.Web.Server.Api.V0;
using ControlR.Web.Server.Authn;
using ControlR.Web.Server.Data;
using ControlR.Web.Server.Data.Entities;
using ControlR.Web.Server.Services;
using ControlR.Web.Server.Services.LogonTokens;
using ControlR.Web.Server.Services.ServiceAccounts;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using CreateInstallerKeyRequestDto = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V0.CreateInstallerKeyRequestDto;
using DeviceResponseDto = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V0.DeviceResponseDto;

namespace ControlR.Web.Server.Tests.V0;

public class ServerPrincipalEndpointTests(ITestOutputHelper testOutput)
{
  private readonly ITestOutputHelper _testOutput = testOutput;

  [Fact]
  public async Task CreateInstallerKey_WithExplicitContext_ReturnsOk()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;

    var tenant = await services.CreateTestTenant();
    var creator = await services.CreateTestUser(tenant.Id, email: "creator@test.local");
    var controller = scope.CreateController<InstallerKeysController>();
    controller.ControllerContext.HttpContext.User = await CreateServerPrincipal(services);

    var result = await controller.Create(new CreateInstallerKeyRequestDto(
      tenant.Id,
      creator.Id,
      CreatorKind.User,
      InstallerKeyType.Persistent));

    Assert.NotNull(result.Result);
    var okResult = Assert.IsType<OkObjectResult>(result.Result);
    var response = Assert.IsType<V0Dtos.CreateInstallerKeyResponseDto>(okResult.Value);
    Assert.NotEqual(Guid.Empty, response.Id);
  }

  [Fact]
  public async Task CreateLogonToken_WithExplicitUserAndTenant_ReturnsOk()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;

    var tenant = await services.CreateTestTenant();
    var user = await services.CreateTestUser(tenant.Id, email: "viewer@test.local");
    var device = await services.CreateTestDevice(tenant.Id);
    var controller = scope.CreateController<LogonTokensController>();
    controller.ControllerContext.HttpContext.User = await CreateServerPrincipal(services);
    controller.ControllerContext.HttpContext.Request.Scheme = "https";
    controller.ControllerContext.HttpContext.Request.Host = new HostString("localhost");

    var result = await controller.CreateForUser(
      services.GetRequiredService<AppDb>(),
      services.GetRequiredService<ILogonTokenProvider>(),
      new CreateLogonTokenForUserRequestDto(device.Id, tenant.Id, user.Id, 15));

    Assert.NotNull(result.Result);
    var okResult = Assert.IsType<OkObjectResult>(result.Result);
    var response = Assert.IsType<V0Dtos.LogonTokenResponseDto>(okResult.Value);
    Assert.Contains($"deviceId={device.Id}", response.DeviceAccessUrl.Query, StringComparison.OrdinalIgnoreCase);
  }

  [Fact]
  public async Task GetDevice_CrossTenant_ReturnsOk()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    await using var appDb = services.GetRequiredService<AppDb>();

    var tenantA = await services.CreateTestTenant("Tenant A");
    var tenantB = await services.CreateTestTenant("Tenant B");
    var deviceA = await services.CreateTestDevice(tenantA.Id);
    var deviceB = await services.CreateTestDevice(tenantB.Id);

    var controller = scope.CreateController<DevicesController>();
    controller.ControllerContext.HttpContext.User = await CreateServerPrincipal(services);
    var agentVersionProvider = services.GetRequiredService<IAgentVersionProvider>();

    var resultA = await controller.GetDevice(appDb, agentVersionProvider, deviceA.Id, TestContext.Current.CancellationToken);
    var okResultA = Assert.IsType<ActionResult<DeviceResponseDto>>(resultA);
    Assert.NotNull(okResultA.Value);
    Assert.Equal(deviceA.Id, okResultA.Value.Id);

    var resultB = await controller.GetDevice(appDb, agentVersionProvider, deviceB.Id, TestContext.Current.CancellationToken);
    var okResultB = Assert.IsType<ActionResult<DeviceResponseDto>>(resultB);
    Assert.NotNull(okResultB.Value);
    Assert.Equal(deviceB.Id, okResultB.Value.Id);
  }

  [Fact]
  public async Task Get_CrossTenant_ReturnsOk()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    await using var appDb = services.GetRequiredService<AppDb>();

    var tenantA = await services.CreateTestTenant("Tenant A");
    var tenantB = await services.CreateTestTenant("Tenant B");
    var deviceA = await services.CreateTestDevice(tenantA.Id);
    var deviceB = await services.CreateTestDevice(tenantB.Id);

    var controller = scope.CreateController<DevicesController>();
    controller.ControllerContext.HttpContext.User = await CreateServerPrincipal(services);

    var agentVersionProvider = services.GetRequiredService<IAgentVersionProvider>();

    var deviceDtos = new List<DeviceResponseDto>();
    await foreach (var dto in controller.Get(appDb, agentVersionProvider, TestContext.Current.CancellationToken))
    {
      deviceDtos.Add(dto);
    }

    Assert.Contains(deviceDtos, d => d.Id == deviceA.Id);
    Assert.Contains(deviceDtos, d => d.Id == deviceB.Id);
  }

  private static async Task<ClaimsPrincipal> CreateServerPrincipal(IServiceProvider services)
  {
    var serviceAccountManager = services.GetRequiredService<IServiceAccountManager>();
    var saResult = await serviceAccountManager.CreateForServer(
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
