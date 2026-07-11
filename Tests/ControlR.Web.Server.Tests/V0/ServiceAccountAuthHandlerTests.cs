using System.Text.Encodings.Web;
using ControlR.Web.Client.Authz;
using ControlR.Web.Server.Authn;
using ControlR.Web.Server.Data;
using ControlR.Web.Server.Extensions;
using ControlR.Web.Server.Services.ServiceAccounts;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ControlR.Web.Server.Tests.V0;

public class ServiceAccountAuthHandlerTests(ITestOutputHelper testOutput)
{
  [Fact]
  public async Task HandleAuthenticateAsync_ShouldFail_WithExpiredCredential()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(testOutput);
    var plainTextSecretKey = string.Empty;

    using (var scope = testApp.CreateScope())
    {
      var services = scope.ServiceProvider;
      var serviceAccountManager = services.GetRequiredService<IServiceAccountManager>();

      var createResult = await serviceAccountManager.CreateForServer(
        "Expired Credential SA",
        null,
        TestContext.Current.CancellationToken);
      Assert.True(createResult.IsSuccess);

      var credentialId = createResult.Value.ServiceAccount.Credentials[0].Id;
      plainTextSecretKey = createResult.Value.PlainTextSecretKey;

      await using var appDb = services.GetRequiredService<AppDb>();
      var credential = await appDb.ServiceAccountCredentials.FindAsync([credentialId], TestContext.Current.CancellationToken);
      Assert.NotNull(credential);
      credential.ExpiresAt = testApp.TimeProvider.GetUtcNow().AddDays(-1);
      await appDb.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    using (var scope = testApp.CreateScope())
    {
      var services = scope.ServiceProvider;
      
      var context = CreateHttpContext(plainTextSecretKey);
      var handler = await CreateHandler(services, context);

      var result = await handler.AuthenticateAsync();

      Assert.False(result.Succeeded);
    }
  }

  [Fact]
  public async Task HandleAuthenticateAsync_ShouldFail_WithInvalidApiKey()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;

    var context = CreateHttpContext("invalid-api-key-format");
    var handler = await CreateHandler(services, context);

    var result = await handler.AuthenticateAsync();

    Assert.False(result.Succeeded);
  }

  [Fact]
  public async Task HandleAuthenticateAsync_ShouldFail_WithRevokedCredential()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var serviceAccountManager = services.GetRequiredService<IServiceAccountManager>();

    var createResult = await serviceAccountManager.CreateForServer(
      "Revocation Test SA",
      null,
      TestContext.Current.CancellationToken);
    Assert.True(createResult.IsSuccess);

    var accountId = createResult.Value.ServiceAccount.Id;
    var credentialId = createResult.Value.ServiceAccount.Credentials[0].Id;
    await serviceAccountManager.RevokeCredential(
      accountId,
      credentialId,
      TestContext.Current.CancellationToken);

    var apiKey = createResult.Value.PlainTextSecretKey;
    var context = CreateHttpContext(apiKey);
    var handler = await CreateHandler(services, context);

    var result = await handler.AuthenticateAsync();

    Assert.False(result.Succeeded);
  }

  [Fact]
  public async Task HandleAuthenticateAsync_ShouldReturnNoResult_WithEmptyHeader()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;

    var context = CreateHttpContext("");
    var handler = await CreateHandler(services, context);

    var result = await handler.AuthenticateAsync();

    Assert.False(result.Succeeded);
    Assert.Null(result.Failure);
  }

  [Fact]
  public async Task HandleAuthenticateAsync_ShouldReturnNoResult_WithMissingHeader()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;

    var context = new DefaultHttpContext();
    var handler = await CreateHandler(services, context);

    var result = await handler.AuthenticateAsync();

    Assert.False(result.Succeeded);
    Assert.Null(result.Failure);
  }

  [Fact]
  public async Task HandleAuthenticateAsync_ShouldSucceed_WithValidApiKey()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var serviceAccountManager = services.GetRequiredService<IServiceAccountManager>();

    var createResult = await serviceAccountManager.CreateForServer(
      "AuthHandlerTest SA",
      null,
      TestContext.Current.CancellationToken);
    Assert.True(createResult.IsSuccess);
    var plainTextSecretKey = createResult.Value.PlainTextSecretKey;

    var context = CreateHttpContext(plainTextSecretKey);
    var handler = await CreateHandler(services, context);

    var result = await handler.AuthenticateAsync();

    Assert.True(result.Succeeded);
    Assert.NotNull(result.Principal);
    Assert.Equal(
      ServiceAccountCredentialAuthenticationSchemeOptions.DefaultScheme,
      result.Principal.Identity?.AuthenticationType);
    Assert.True(result.Principal.Identity?.IsAuthenticated);

    Assert.Equal(
      PrincipalClaimTypes.ServerServiceAccount,
      result.Principal.FindFirst(PrincipalClaimTypes.PrincipalType)?.Value);
    Assert.NotNull(result.Principal.FindFirst(PrincipalClaimTypes.PrincipalId)?.Value);
    Assert.NotNull(result.Principal.FindFirst(PrincipalClaimTypes.CredentialId)?.Value);
    Assert.Equal(
      PrincipalClaimTypes.ServiceAccountCredentialMethod,
      result.Principal.FindFirst(UserClaimTypes.AuthenticationMethod)?.Value);

    Assert.Null(result.Principal.FindFirst(UserClaimTypes.TenantId));
    Assert.Null(result.Principal.FindFirst(UserClaimTypes.UserId));

    Assert.True(result.Principal.IsServerPrincipal());
  }

  private static DefaultHttpContext CreateHttpContext(string? apiKey)
  {
    var context = new DefaultHttpContext();

    if (!string.IsNullOrEmpty(apiKey))
    {
      context.Request.Headers[ServiceAccountCredentialAuthenticationSchemeOptions.DefaultHeaderName] = apiKey;
    }

    return context;
  }

  private async Task<ServiceAccountCredentialAuthenticationHandler> CreateHandler(
    IServiceProvider services,
    HttpContext context)
  {
    var options = services.GetRequiredService<IOptionsMonitor<ServiceAccountCredentialAuthenticationSchemeOptions>>();
    var loggerFactory = services.GetRequiredService<ILoggerFactory>();
    var serviceAccountManager = services.GetRequiredService<IServiceAccountManager>();

    var scheme = new AuthenticationScheme(
      ServiceAccountCredentialAuthenticationSchemeOptions.DefaultScheme,
      ServiceAccountCredentialAuthenticationSchemeOptions.DefaultScheme,
      typeof(ServiceAccountCredentialAuthenticationHandler));

    var handler = new ServiceAccountCredentialAuthenticationHandler(
      UrlEncoder.Default,
      serviceAccountManager,
      loggerFactory,
      options);

    await handler.InitializeAsync(scheme, context);

    return handler;
  }
}