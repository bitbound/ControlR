using System.Net;
using System.Net.Http.Json;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1;
using ControlR.Web.Server.Authn;
using ControlR.Web.Server.Services;
using ControlR.Web.Server.Services.ServiceAccounts;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace ControlR.Web.Server.Tests;

public class AuthIntegrationTests(ITestOutputHelper testOutput)
{
  private readonly ITestOutputHelper _testOutput = testOutput;

  [Fact]
  public async Task InteractiveLogin_ReturnsBearerTokens()
  {
    var settings = new Dictionary<string, string?>
    {
      ["AppOptions:EnableInteractiveBearerLogin"] = "true",
    };

    using var testServer = await TestWebServerBuilder.CreateTestServer(_testOutput, settings: settings);
    using var httpClient = testServer.Factory.CreateClient();

    var tenant = await testServer.Services.CreateTestTenant();
    var email = $"interactive-login-{Guid.NewGuid():N}@t.local";
    await testServer.Services.CreateTestUser(tenant.Id, email);

    var loginData = new LoginRequestDto(email, "T3stP@ssw0rd!");
    var loginResponse = await httpClient.PostAsJsonAsync(
      $"{HttpConstants.Internal.AuthEndpoint}/interactive-login",
      loginData,
      TestContext.Current.CancellationToken);

    loginResponse.EnsureSuccessStatusCode();
    var loginResult = await loginResponse.Content.ReadFromJsonAsync<InteractiveLoginResponseDto>(
      TestContext.Current.CancellationToken);
    Assert.NotNull(loginResult);
    Assert.NotNull(loginResult.Tokens);
    Assert.NotEmpty(loginResult.Tokens.AccessToken);
    Assert.Equal("Bearer", loginResult.Tokens.TokenType);
  }

  [Fact]
  public async Task PatAuth_AccessDeniedWithInvalidToken()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(_testOutput);
    using var httpClient = testServer.Factory.CreateClient();

    httpClient.DefaultRequestHeaders.Add(
      PersonalAccessTokenAuthenticationSchemeOptions.DefaultHeaderName,
      "invalid-pat-token");

    var response = await httpClient.GetAsync(
      $"{HttpConstants.Internal.AuthEndpoint}/me",
      TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
  }

  [Fact]
  public async Task PatAuth_AuthenticatedAccess()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(_testOutput);
    using var httpClient = testServer.Factory.CreateClient();

    var tenant = await testServer.Services.CreateTestTenant();
    var user = await testServer.Services.CreateTestUser(tenant.Id, email: "pat-auth@t.local");

    var patManager = testServer.Services.GetRequiredService<IPersonalAccessTokenManager>();
    var createResult = await patManager.CreateToken(
      new CreatePersonalAccessTokenRequestDto("Test PAT"),
      user.Id);
    Assert.True(createResult.IsSuccess, $"PAT creation failed: {createResult.Reason}");

    httpClient.DefaultRequestHeaders.Add(
      PersonalAccessTokenAuthenticationSchemeOptions.DefaultHeaderName,
      createResult.Value.PlainTextToken);

    var response = await httpClient.GetAsync(
      $"{HttpConstants.Internal.AuthEndpoint}/me",
      TestContext.Current.CancellationToken);

    Assert.True(response.IsSuccessStatusCode,
      $"PAT auth access failed: {response.StatusCode}");
  }

  [Fact]
  public async Task ServiceAcctAuth_AccessDeniedWithInvalidKey()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(_testOutput);
    using var httpClient = testServer.Factory.CreateClient();

    httpClient.DefaultRequestHeaders.Add(
      "X-Api-Key",
      "invalid-key-format");

    var response = await httpClient.PostAsJsonAsync(
      HttpConstants.V1.TenantsEndpoint,
      new CreateTenantRequestDto("Auth Test Tenant"),
      TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
  }

  [Fact]
  public async Task ServiceAcctAuth_AccessV1Endpoint()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(_testOutput);
    using var httpClient = testServer.Factory.CreateClient();

    var serviceAccountManager = testServer.Services.GetRequiredService<IServiceAccountManager>();
    var createResult = await serviceAccountManager.CreateForServer(
      "V1 Auth Test SA",
      null,
      TestContext.Current.CancellationToken);
    Assert.True(createResult.IsSuccess);

    httpClient.DefaultRequestHeaders.Add(
      "X-Api-Key",
      createResult.Value.PlainTextSecretKey);

    var response = await httpClient.PostAsJsonAsync(
      HttpConstants.V1.TenantsEndpoint,
      new CreateTenantRequestDto("Auth Test Tenant"),
      TestContext.Current.CancellationToken);

    Assert.True(response.IsSuccessStatusCode,
      $"Service account auth failed: {response.StatusCode}");
    var createdResult = await response.Content.ReadFromJsonAsync<CreateTenantResponseDto>(
      TestContext.Current.CancellationToken);
    Assert.NotNull(createdResult);
    Assert.NotEqual(Guid.Empty, createdResult.TenantId);
  }

  [Fact]
  public async Task UnauthenticatedAccess_Returns401()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(_testOutput);
    using var httpClient = testServer.Factory.CreateClient();

    var response = await httpClient.GetAsync(
      $"{HttpConstants.Internal.AuthEndpoint}/me",
      TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
  }
}