using System.Security.Claims;
using ControlR.Web.Client.Authz;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V0;
using ControlR.Web.Server.Api.V0;
using V0DevicesController = ControlR.Web.Server.Api.V0.DevicesController;
using ControlR.Web.Server.Authn;
using ControlR.Web.Server.Data;
using ControlR.Web.Server.Data.Entities;
using ControlR.Web.Server.Services;
using ControlR.Web.Server.Services.ServiceAccounts;
using ControlR.Web.Server.Startup;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using DeviceResponseDto = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V0.DeviceResponseDto;

namespace ControlR.Web.Server.Tests;

public class BootstrapServiceAccountTests(ITestOutputHelper testOutput)
{
  private const string AccountName = "test-bootstrap-sa";
  private const string TokenId = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee";
  private const string TokenSecret = "test-bootstrap-secret-key-thirty-two-chars!";

  private readonly ITestOutputHelper _testOutputHelper = testOutput;

  [Fact]
  public async Task Bootstrap_CreatesServerServiceAccount()
  {
    var config = new Dictionary<string, string?>
    {
      ["Bootstrap:ServerServiceAccountName"] = AccountName,
      ["Bootstrap:ServerServiceAccountTokenId"] = TokenId,
      ["Bootstrap:ServerServiceAccountTokenSecret"] = TokenSecret,
    };

    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper, extraConfiguration: config);
    await testApp.App.BootstrapServerServiceAccount();

    using var scope = testApp.CreateScope();
    var appDb = scope.ServiceProvider.GetRequiredService<AppDb>();

    var account = await appDb.ServiceAccounts
      .IgnoreQueryFilters()
      .Include(x => x.Credentials)
      .FirstOrDefaultAsync(x => x.Name == AccountName, TestContext.Current.CancellationToken);

    Assert.NotNull(account);
    Assert.Equal(ControlR.Web.Server.Data.Enums.ServiceAccountKind.Server, account.Kind);
    Assert.Null(account.TenantId);
    Assert.True(account.IsEnabled);

    Assert.NotEmpty(account.Credentials);
    Assert.Single(account.Credentials);
    var credential = account.Credentials[0];
    Assert.NotEqual(Guid.Empty, credential.Id);
    Assert.NotEmpty(credential.HashedSecret);
    Assert.Null(credential.RevokedAt);
  }

  [Fact]
  public async Task Bootstrap_CredentialCanAuthenticate()
  {
    var config = new Dictionary<string, string?>
    {
      ["Bootstrap:ServerServiceAccountName"] = AccountName,
      ["Bootstrap:ServerServiceAccountTokenId"] = TokenId,
      ["Bootstrap:ServerServiceAccountTokenSecret"] = TokenSecret,
    };

    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper, extraConfiguration: config);
    await testApp.App.BootstrapServerServiceAccount();

    using var scope = testApp.CreateScope();
    var appDb = scope.ServiceProvider.GetRequiredService<AppDb>();
    var credential = await appDb.ServiceAccountCredentials
      .IgnoreQueryFilters()
      .FirstAsync(TestContext.Current.CancellationToken);

    var hexId = Convert.ToHexString(credential.Id.ToByteArray());
    var apiKey = $"{hexId}:{TokenSecret}";

    var serviceAccountManager = scope.ServiceProvider.GetRequiredService<IServiceAccountManager>();
    var validationResult = await serviceAccountManager.ValidateCredential(apiKey, TestContext.Current.CancellationToken);

    Assert.True(validationResult.IsSuccess);
    Assert.NotNull(validationResult.Value);
    Assert.Equal(credential.Id, validationResult.Value.Credential.Id);
    Assert.Equal(AccountName, validationResult.Value.ServiceAccount.Name);
  }

  [Fact]
  public async Task Bootstrap_SkipsWhenAccountAlreadyExists()
  {
    var config = new Dictionary<string, string?>
    {
      ["Bootstrap:ServerServiceAccountName"] = AccountName,
      ["Bootstrap:ServerServiceAccountTokenId"] = TokenId,
      ["Bootstrap:ServerServiceAccountTokenSecret"] = TokenSecret,
    };

    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper, extraConfiguration: config);
    await testApp.App.BootstrapServerServiceAccount();
    await testApp.App.BootstrapServerServiceAccount();

    using var scope = testApp.CreateScope();
    var appDb = scope.ServiceProvider.GetRequiredService<AppDb>();

    var count = await appDb.ServiceAccounts
      .IgnoreQueryFilters()
      .CountAsync(x => x.Name == AccountName, TestContext.Current.CancellationToken);

    Assert.Equal(1, count);
  }

  [Fact]
  public async Task Bootstrap_WithKey_RawApiKeyHeaderRoundTrips()
  {
    var config = new Dictionary<string, string?>
    {
      ["Bootstrap:ServerServiceAccountName"] = AccountName,
      ["Bootstrap:ServerServiceAccountTokenId"] = TokenId,
      ["Bootstrap:ServerServiceAccountTokenSecret"] = TokenSecret,
    };

    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper, extraConfiguration: config);
    await testApp.App.BootstrapServerServiceAccount();

    using var scope = testApp.CreateScope();
    var appDb = scope.ServiceProvider.GetRequiredService<AppDb>();
    var credential = await appDb.ServiceAccountCredentials
      .IgnoreQueryFilters()
      .FirstAsync(TestContext.Current.CancellationToken);

    var hexId = Convert.ToHexString(credential.Id.ToByteArray());
    Assert.Equal(32, hexId.Length);

    var configId = Guid.Parse(TokenId);
    Assert.Equal(configId, credential.Id);

    var apiKey = $"{hexId}:{TokenSecret}";
    var manager = scope.ServiceProvider.GetRequiredService<IServiceAccountManager>();
    var result = await manager.ValidateCredential(apiKey, TestContext.Current.CancellationToken);
    Assert.True(result.IsSuccess);
  }

  [Fact]
  public async Task Bootstrap_WithNoConfig_DoesNothing()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    await testApp.App.BootstrapServerServiceAccount();

    using var scope = testApp.CreateScope();
    var appDb = scope.ServiceProvider.GetRequiredService<AppDb>();

    var count = await appDb.ServiceAccounts.IgnoreQueryFilters().CountAsync(TestContext.Current.CancellationToken);
    Assert.Equal(0, count);
  }

  [Fact]
  public async Task Bootstrap_WithPartialConfig_Throws()
  {
    var config = new Dictionary<string, string?>
    {
      ["Bootstrap:ServerServiceAccountName"] = AccountName,
    };

    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper, extraConfiguration: config);
    await Assert.ThrowsAsync<InvalidOperationException>(
      () => testApp.App.BootstrapServerServiceAccount());
  }

  [Fact]
  public async Task NonServerPrincipal_DeniedServiceAccountManagement()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;

    var tenant = await services.CreateTestTenant();
    var user = await services.CreateTestUser(tenant.Id, email: "denied@test.local",
      roles: RoleNames.TenantAdministrator);

    var controller = scope.CreateController<ServiceAccountsController>();
    await controller.SetControllerUser(user, services.GetRequiredService<UserManager<AppUser>>());

    var manager = services.GetRequiredService<IServiceAccountManager>();

    var createResult = await controller.Create(
      new CreateServiceAccountRequestDto("Should Not Work", null),
      TestContext.Current.CancellationToken);

    var forbidResult = Assert.IsType<ForbidResult>(createResult.Result);
  }

  [Fact]
  public async Task ServerPrincipalCanGetSingleDeviceFromAnyTenant()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var appDb = services.GetRequiredService<AppDb>();

    var tenantA = await services.CreateTestTenant("Tenant A");
    var tenantB = await services.CreateTestTenant("Tenant B");
    var deviceA = await services.CreateTestDevice(tenantA.Id);
    var deviceB = await services.CreateTestDevice(tenantB.Id);

    var serviceAccountManager = services.GetRequiredService<IServiceAccountManager>();
    var saResult = await serviceAccountManager.CreateServer(
      "CrossTenant SA 2",
      null,
      TestContext.Current.CancellationToken);
    Assert.True(saResult.IsSuccess);

    var serverPrincipal = new ClaimsPrincipal(new ClaimsIdentity(
    [
      new Claim(PrincipalClaimTypes.PrincipalType, PrincipalClaimTypes.ServerServiceAccount),
      new Claim(PrincipalClaimTypes.PrincipalId, saResult.Value.ServiceAccount.Id.ToString()),
      new Claim(UserClaimTypes.AuthenticationMethod, PrincipalClaimTypes.ServiceAccountCredentialMethod),
      new Claim(PrincipalClaimTypes.CredentialId, saResult.Value.ServiceAccount.Credentials[0].Id.ToString()),
    ], "TestAuth"));

    var controller = scope.CreateController<V0DevicesController>();
    controller.ControllerContext.HttpContext.User = serverPrincipal;
    var authorizationService = services.GetRequiredService<IAuthorizationService>();
    var agentVersionProvider = services.GetRequiredService<IAgentVersionProvider>();

    var resultA = await controller.GetDevice(appDb, authorizationService, agentVersionProvider, deviceA.Id);
    var okResultA = Assert.IsType<ActionResult<DeviceResponseDto>>(resultA);
    Assert.NotNull(okResultA.Value);
    Assert.Equal(deviceA.Id, okResultA.Value.Id);

    var resultB = await controller.GetDevice(appDb, authorizationService, agentVersionProvider, deviceB.Id);
    var okResultB = Assert.IsType<ActionResult<DeviceResponseDto>>(resultB);
    Assert.NotNull(okResultB.Value);
    Assert.Equal(deviceB.Id, okResultB.Value.Id);
  }

  [Fact]
  public async Task ServerPrincipalCanListDevicesAcrossTenants()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var appDb = services.GetRequiredService<AppDb>();

    var tenantA = await services.CreateTestTenant("Tenant A");
    var tenantB = await services.CreateTestTenant("Tenant B");
    var deviceA = await services.CreateTestDevice(tenantA.Id);
    var deviceB = await services.CreateTestDevice(tenantB.Id);

    var serviceAccountManager = services.GetRequiredService<IServiceAccountManager>();
    var saResult = await serviceAccountManager.CreateServer(
      "CrossTenant SA",
      null,
      TestContext.Current.CancellationToken);
    Assert.True(saResult.IsSuccess);

    var serverPrincipal = new ClaimsPrincipal(new ClaimsIdentity(
    [
      new Claim(PrincipalClaimTypes.PrincipalType, PrincipalClaimTypes.ServerServiceAccount),
      new Claim(PrincipalClaimTypes.PrincipalId, saResult.Value.ServiceAccount.Id.ToString()),
      new Claim(UserClaimTypes.AuthenticationMethod, PrincipalClaimTypes.ServiceAccountCredentialMethod),
      new Claim(PrincipalClaimTypes.CredentialId, saResult.Value.ServiceAccount.Credentials[0].Id.ToString()),
    ], "TestAuth"));

    var controller = scope.CreateController<V0DevicesController>();
    controller.ControllerContext.HttpContext.User = serverPrincipal;

    var agentVersionProvider = services.GetRequiredService<IAgentVersionProvider>();

    var deviceDtos = new List<DeviceResponseDto>();
    await foreach (var dto in controller.Get(appDb, agentVersionProvider))
    {
      deviceDtos.Add(dto);
    }

    Assert.Contains(deviceDtos, d => d.Id == deviceA.Id);
    Assert.Contains(deviceDtos, d => d.Id == deviceB.Id);
  }

  [Fact]
  public async Task ServiceAccountLifecycle_CreateRevokeDelete()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var manager = services.GetRequiredService<IServiceAccountManager>();

    var createResult = await manager.CreateServer(
      "Lifecycle SA",
      "Created by test",
      TestContext.Current.CancellationToken);
    Assert.True(createResult.IsSuccess);

    var accountId = createResult.Value.ServiceAccount.Id;
    var credentialId = createResult.Value.ServiceAccount.Credentials[0].Id;
    var apiKey = createResult.Value.PlainTextSecretKey;

    var validateResult = await manager.ValidateCredential(apiKey, TestContext.Current.CancellationToken);
    Assert.True(validateResult.IsSuccess);
    Assert.Equal(accountId, validateResult.Value.ServiceAccount.Id);

    var addCredResult = await manager.AddCredential(
      accountId,
      "Secondary key",
      TestContext.Current.CancellationToken);
    Assert.True(addCredResult.IsSuccess);
    var secondApiKey = addCredResult.Value.PlainTextSecretKey;

    await manager.RevokeCredential(accountId, credentialId, TestContext.Current.CancellationToken);

    var shouldFail = await manager.ValidateCredential(apiKey, TestContext.Current.CancellationToken);
    Assert.False(shouldFail.IsSuccess);

    var shouldPass = await manager.ValidateCredential(secondApiKey, TestContext.Current.CancellationToken);
    Assert.True(shouldPass.IsSuccess);

    await manager.Delete(accountId, TestContext.Current.CancellationToken);

    var allAccounts = await manager.GetAllServer(TestContext.Current.CancellationToken);
    Assert.DoesNotContain(allAccounts, a => a.Id == accountId);
  }
}