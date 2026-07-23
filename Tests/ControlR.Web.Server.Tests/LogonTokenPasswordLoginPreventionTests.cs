using System.Net;
using System.Net.Http.Json;
using ControlR.Web.Server.Data.Enums;
using ControlR.Web.Server.Services.LogonTokens;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace ControlR.Web.Server.Tests;

public class LogonTokenPasswordLoginPreventionTests(ITestOutputHelper testOutput)
{
  private readonly ITestOutputHelper _testOutput = testOutput;

  public static TheoryData<string, string> SamplePasswords()
  {
    var data = new TheoryData<string, string>
    {
      { "", "empty" },
      { " ", "whitespace" },
      { "password", "common" },
      { "T3stP@ssw0rd!", "known-test-password" },
      { "correct horse battery staple", "passphrase" },
      { Guid.NewGuid().ToString("N"), "random-guid" }
    };
    return data;
  }

  [Fact]
  public async Task ExternalUserCreatedByLogonToken_HasNoPasswordHash()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.App.Services.CreateScope();
    var logonTokenProvider = scope.ServiceProvider.GetRequiredService<ILogonTokenProvider>();
    var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<Data.Entities.AppUser>>();

    var tenant = await testApp.App.Services.CreateTestTenant();
    var userCorrelationId = $"test-{Guid.NewGuid():N}";

    var result = await logonTokenProvider.CreateTokenForExternal(
      Guid.NewGuid(),
      tenant.Id,
      userCorrelationId,
      cancellationToken: TestContext.Current.CancellationToken);

    Assert.True(result.IsSuccess);
    var externalUser = await userManager.FindByIdAsync(result.Value.UserId.ToString());
    Assert.NotNull(externalUser);
    Assert.Equal(AccountType.ExternalUser, externalUser.AccountType);
    Assert.Null(externalUser.PasswordHash);
    Assert.False(externalUser.EmailConfirmed);
  }

  [Theory]
  [MemberData(nameof(SamplePasswords))]
  public async Task InteractiveLogin_WithExternalUserAndAnyPassword_ReturnsUnauthorized(
    string password,
    string _)
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(
      _testOutput,
      settings: new Dictionary<string, string?>
      {
        ["AppOptions:EnableInteractiveBearerLogin"] = "true"
      });

    var tenant = await testServer.Services.CreateTestTenant();
    var userCorrelationId = $"test-{Guid.NewGuid():N}";

    var createResult = await testServer.Services
      .GetRequiredService<ILogonTokenProvider>()
      .CreateTokenForExternal(
        Guid.NewGuid(),
        tenant.Id,
        userCorrelationId,
        cancellationToken: TestContext.Current.CancellationToken);

    Assert.True(createResult.IsSuccess);
    var externalEmail = $"ext-{userCorrelationId}@controlr.local";

    using var client = testServer.Factory.CreateClient();
    using var response = await client.PostAsJsonAsync(
      $"{HttpConstants.Internal.AuthEndpoint}/interactive-login",
      new InternalDtos.LoginRequestDto(externalEmail, password),
      TestContext.Current.CancellationToken);

    Assert.Equal(
      HttpStatusCode.Unauthorized,
      response.StatusCode);

    var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    Assert.DoesNotContain("accessToken", body, StringComparison.OrdinalIgnoreCase);
    Assert.DoesNotContain("refreshToken", body, StringComparison.OrdinalIgnoreCase);
  }

  [Fact]
  public async Task InteractiveLogin_WithExternalUserEmailAndCorrelationIdAsPassword_ReturnsNoTokens()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(
      _testOutput,
      settings: new Dictionary<string, string?>
      {
        ["AppOptions:EnableInteractiveBearerLogin"] = "true"
      });

    var tenant = await testServer.Services.CreateTestTenant();
    var userCorrelationId = $"test-{Guid.NewGuid():N}";

    var createResult = await testServer.Services
      .GetRequiredService<ILogonTokenProvider>()
      .CreateTokenForExternal(
        Guid.NewGuid(),
        tenant.Id,
        userCorrelationId,
        cancellationToken: TestContext.Current.CancellationToken);

    Assert.True(createResult.IsSuccess);
    var externalEmail = $"ext-{userCorrelationId}@controlr.local";

    using var client = testServer.Factory.CreateClient();
    using var response = await client.PostAsJsonAsync(
      $"{HttpConstants.Internal.AuthEndpoint}/interactive-login",
      new InternalDtos.LoginRequestDto(externalEmail, userCorrelationId),
      TestContext.Current.CancellationToken);

    Assert.Equal(
      HttpStatusCode.Unauthorized,
      response.StatusCode);

    var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    Assert.DoesNotContain("accessToken", body, StringComparison.OrdinalIgnoreCase);
  }

  [Fact]
  public async Task InteractiveLogin_WithExternalUserEmailAndUsernameAsPassword_ReturnsNoTokens()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(
      _testOutput,
      settings: new Dictionary<string, string?>
      {
        ["AppOptions:EnableInteractiveBearerLogin"] = "true"
      });

    var tenant = await testServer.Services.CreateTestTenant();
    var userCorrelationId = $"test-{Guid.NewGuid():N}";

    var createResult = await testServer.Services
      .GetRequiredService<ILogonTokenProvider>()
      .CreateTokenForExternal(
        Guid.NewGuid(),
        tenant.Id,
        userCorrelationId,
        cancellationToken: TestContext.Current.CancellationToken);

    Assert.True(createResult.IsSuccess);
    var externalUsername = $"ext-{userCorrelationId}";
    var externalEmail = $"{externalUsername}@controlr.local";

    using var client = testServer.Factory.CreateClient();
    using var response = await client.PostAsJsonAsync(
      $"{HttpConstants.Internal.AuthEndpoint}/interactive-login",
      new InternalDtos.LoginRequestDto(externalEmail, externalUsername),
      TestContext.Current.CancellationToken);

    Assert.Equal(
      HttpStatusCode.Unauthorized,
      response.StatusCode);

    var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    Assert.DoesNotContain("accessToken", body, StringComparison.OrdinalIgnoreCase);
  }

  [Fact]
  public async Task InteractiveLogin_WithNormalUserAndCorrectPassword_ReturnsTokens()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(
      _testOutput,
      settings: new Dictionary<string, string?>
      {
        ["AppOptions:EnableInteractiveBearerLogin"] = "true"
      });

    var tenant = await testServer.Services.CreateTestTenant();
    const string password = "T3stP@ssw0rd!";
    var user = await testServer.Services.CreateTestUser(tenant.Id);

    using var client = testServer.Factory.CreateClient();
    using var response = await client.PostAsJsonAsync(
      $"{HttpConstants.Internal.AuthEndpoint}/interactive-login",
      new InternalDtos.LoginRequestDto(user.Email!, password),
      TestContext.Current.CancellationToken);

    response.EnsureSuccessStatusCode();
    var payload = await response.Content.ReadFromJsonAsync<InternalDtos.InteractiveLoginResponseDto>(
      TestContext.Current.CancellationToken);
    Assert.NotNull(payload);
    Assert.NotNull(payload.Tokens);
    Assert.NotEmpty(payload.Tokens.AccessToken);
  }
}