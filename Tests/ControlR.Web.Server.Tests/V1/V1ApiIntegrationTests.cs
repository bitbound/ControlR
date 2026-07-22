using System.Net.Http.Json;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1;
using ControlR.Web.Server.Authn;
using ControlR.Web.Server.Services.ServiceAccounts;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace ControlR.Web.Server.Tests.V1;

public class V1ApiIntegrationTests(ITestOutputHelper testOutput)
{
  [Fact]
  public async Task CreateInstallerKey_ViaHttp_ReturnsOk()
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
      HttpConstants.V1.InstallerKeysEndpoint,
      request,
      TestContext.Current.CancellationToken);

    Assert.True(response.IsSuccessStatusCode, $"Response: {response.StatusCode}");
    var result = await response.Content.ReadFromJsonAsync<CreateInstallerKeyResponseDto>(
      TestContext.Current.CancellationToken);
    Assert.NotNull(result);
    Assert.NotEqual(Guid.Empty, result.Id);
  }

  [Fact]
  public async Task CreateLogonToken_ViaHttp_ReturnsOk()
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

    var request = new CreateLogonTokenForUserRequestDto(device.Id, tenant.Id, user.Id, ExpirationMinutes: 15);
    var response = await httpClient.PostAsJsonAsync(
      $"{HttpConstants.V1.LogonTokensEndpoint}/user",
      request,
      TestContext.Current.CancellationToken);

    Assert.True(response.IsSuccessStatusCode, $"Response: {response.StatusCode}");
    var result = await response.Content.ReadFromJsonAsync<LogonTokenResponseDto>(
      TestContext.Current.CancellationToken);
    Assert.NotNull(result);
    Assert.Contains($"deviceId={device.Id}", result.DeviceAccessUrl.Query, StringComparison.OrdinalIgnoreCase);
  }

  [Fact]
  public async Task UnauthenticatedAccess_V1Endpoint_Returns401()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(testOutput);
    using var httpClient = await testServer.GetHttpClient();

    var tenant = await testServer.Services.CreateTestTenant();
    var request = new CreateInstallerKeyRequestDto(tenant.Id, Guid.NewGuid(), CreatorKind.User, InstallerKeyType.Persistent);
    var response = await httpClient.PostAsJsonAsync(
      HttpConstants.V1.InstallerKeysEndpoint,
      request,
      TestContext.Current.CancellationToken);

    Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
  }
}
