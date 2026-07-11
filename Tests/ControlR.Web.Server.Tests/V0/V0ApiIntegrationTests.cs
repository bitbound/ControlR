using System.Net.Http.Json;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V0;
using ControlR.Web.Server.Authn;
using ControlR.Web.Server.Services.ServiceAccounts;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace ControlR.Web.Server.Tests.V0;

public class V0ApiIntegrationTests(ITestOutputHelper testOutput)
{
  [Fact]
  public async Task ServerPrincipal_CreateInstallerKey_ViaHttp()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(testOutput);
    using var httpClient = await testServer.GetHttpClient();

    var saManager = testServer.Services.GetRequiredService<IServiceAccountManager>();
    var saResult = await saManager.CreateForServer(
      "Integration Test SA - IK",
      null,
      TestContext.Current.CancellationToken);
    Assert.True(saResult.IsSuccess);
    httpClient.DefaultRequestHeaders.Add(
      ServiceAccountCredentialAuthenticationSchemeOptions.DefaultHeaderName,
      saResult.Value.PlainTextSecretKey);

    var tenant = await testServer.Services.CreateTestTenant();
    var user = await testServer.Services.CreateTestUser(tenant.Id, email: "ik-int@test.local");

    var request = new CreateInstallerKeyRequestDto(tenant.Id, user.Id, CreatorKind.User, InstallerKeyType.Persistent);
    var response = await httpClient.PostAsJsonAsync(
      HttpConstants.V0.InstallerKeysEndpoint,
      request,
      TestContext.Current.CancellationToken);

    Assert.True(response.IsSuccessStatusCode, $"Response: {response.StatusCode}");
    var result = await response.Content.ReadFromJsonAsync<CreateInstallerKeyResponseDto>(
      TestContext.Current.CancellationToken);
    Assert.NotNull(result);
    Assert.NotEqual(Guid.Empty, result.Id);
  }

  [Fact]
  public async Task ServerPrincipal_CreateLogonToken_ViaHttp()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(testOutput);
    using var httpClient = await testServer.GetHttpClient();

    var saManager = testServer.Services.GetRequiredService<IServiceAccountManager>();
    var saResult = await saManager.CreateForServer(
      "Integration Test SA - LT",
      null,
      TestContext.Current.CancellationToken);
    Assert.True(saResult.IsSuccess);
    httpClient.DefaultRequestHeaders.Add(
      ServiceAccountCredentialAuthenticationSchemeOptions.DefaultHeaderName,
      saResult.Value.PlainTextSecretKey);

    var tenant = await testServer.Services.CreateTestTenant();
    var user = await testServer.Services.CreateTestUser(tenant.Id, email: "lt-int@test.local");
    var device = await testServer.Services.CreateTestDevice(tenant.Id);

    var request = new CreateLogonTokenRequestDto(device.Id, tenant.Id, user.Id, null, LogonTokenKind.User, 15);
    var response = await httpClient.PostAsJsonAsync(
      HttpConstants.V0.LogonTokensEndpoint,
      request,
      TestContext.Current.CancellationToken);

    Assert.True(response.IsSuccessStatusCode, $"Response: {response.StatusCode}");
    var result = await response.Content.ReadFromJsonAsync<LogonTokenResponseDto>(
      TestContext.Current.CancellationToken);
    Assert.NotNull(result);
    Assert.Contains($"deviceId={device.Id}", result.DeviceAccessUrl.Query, StringComparison.OrdinalIgnoreCase);
  }

  [Fact]
  public async Task UnauthenticatedAccess_V0Endpoint_Returns401()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(testOutput);
    using var httpClient = await testServer.GetHttpClient();

    var tenant = await testServer.Services.CreateTestTenant();
    var request = new CreateInstallerKeyRequestDto(tenant.Id, Guid.NewGuid(), CreatorKind.User, InstallerKeyType.Persistent);
    var response = await httpClient.PostAsJsonAsync(
      HttpConstants.V0.InstallerKeysEndpoint,
      request,
      TestContext.Current.CancellationToken);

    Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
  }
}
