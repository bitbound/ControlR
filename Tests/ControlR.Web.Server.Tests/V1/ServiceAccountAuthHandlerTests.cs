using System.Text.Encodings.Web;
using ControlR.Web.Server.Authn;
using ControlR.Web.Server.Data;
using ControlR.Web.Server.Extensions;
using ControlR.Web.Server.Options;
using ControlR.Web.Server.Services.ServiceAccounts;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ControlR.Web.Server.Tests.V1;

public class ServiceAccountAuthHandlerTests(ITestOutputHelper testOutput)
{
  [Theory]
  [InlineData(false, false, false, true)]  // healthy: account enabled, credential active, not expired -> success
  [InlineData(true, false, false, false)] // account disabled blocks auth
  [InlineData(false, true, false, false)] // revoked credential blocks auth
  [InlineData(false, false, true, false)] // expired credential blocks auth
  [InlineData(true, true, false, false)] // disabled + revoked
  [InlineData(true, false, true, false)] // disabled + expired
  [InlineData(false, true, true, false)] // revoked + expired
  [InlineData(true, true, true, false)]  // all three flags set
  public async Task HandleAuthenticateAsync_StateMatrix_AppliesExpectedOutcome(
    bool accountDisabled,
    bool credentialRevoked,
    bool credentialExpired,
    bool expectSucceeded)
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(testOutput);

    Guid accountId;
    Guid credentialId;
    string plainTextSecretKey;
    using (var scope = testApp.CreateScope())
    {
      var manager = scope.ServiceProvider.GetRequiredService<IServiceAccountManager>();

      var createResult = await manager.CreateForServer(
        $"StateMatrix {Guid.NewGuid():N}",
        null,
        ServiceAccountAccessMode.Unrestricted, TestContext.Current.CancellationToken);
      Assert.True(createResult.IsSuccess);
      accountId = createResult.Value.Id;

      var credResult = await manager.AddCredentialForServer(
        accountId,
        "Test Credential",
        expiresAt: null,
        TestActors.User(),
        TestContext.Current.CancellationToken);
      Assert.True(credResult.IsSuccess);
      credentialId = credResult.Value.Credential.Id;
      plainTextSecretKey = credResult.Value.PlainTextSecretKey;
    }

    using (var innerScope = testApp.CreateScope())
    {
      await using var db = innerScope.ServiceProvider.GetRequiredService<AppDb>();

      if (accountDisabled)
      {
        var account = await db.ServiceAccounts.FirstAsync(
          x => x.Id == accountId, TestContext.Current.CancellationToken);
        account.IsEnabled = false;
      }

      var credential = await db.ServiceAccountCredentials.FirstAsync(
        x => x.Id == credentialId, TestContext.Current.CancellationToken);

      if (credentialRevoked)
      {
        credential.RevokedAt = testApp.TimeProvider.GetUtcNow();
      }

      if (credentialExpired)
      {
        credential.ExpiresAt = testApp.TimeProvider.GetUtcNow().AddDays(-1);
      }

      await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    // Authenticate from a fresh scope (mirrors production request scoping) so
    // ValidateCredential reads the updated state rather than stale tracked entities.
    using var authScope = testApp.CreateScope();
    var context = CreateHttpContext(plainTextSecretKey);
    var handler = await CreateHandler(authScope.ServiceProvider, context);

    var result = await handler.AuthenticateAsync();

    Assert.Equal(expectSucceeded, result.Succeeded);
  }

  [Fact]
  public async Task HandleAuthenticateAsync_WithDisabledAccount_ShouldFail()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(testOutput);

    Guid accountId;
    string plainTextSecretKey;
    using (var scope = testApp.CreateScope())
    {
      var manager = scope.ServiceProvider.GetRequiredService<IServiceAccountManager>();

      var createResult = await manager.CreateForServer(
        "Disabled Account SA",
        null,
        ServiceAccountAccessMode.Unrestricted, TestContext.Current.CancellationToken);
      Assert.True(createResult.IsSuccess);
      accountId = createResult.Value.Id;

      var credResult = await manager.AddCredentialForServer(
        accountId,
        "Test Credential",
        expiresAt: null,
        TestActors.User(),
        TestContext.Current.CancellationToken);
      Assert.True(credResult.IsSuccess);
      plainTextSecretKey = credResult.Value.PlainTextSecretKey;
    }

    using (var innerScope = testApp.CreateScope())
    {
      await using var db = innerScope.ServiceProvider.GetRequiredService<AppDb>();
      var account = await db.ServiceAccounts.FirstAsync(
        x => x.Id == accountId, TestContext.Current.CancellationToken);
      account.IsEnabled = false;
      await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    // Authenticate from a fresh scope (mirrors production request scoping) so
    // ValidateCredential reads the updated state rather than stale tracked entities.
    using var authScope = testApp.CreateScope();
    var context = CreateHttpContext(plainTextSecretKey);
    var handler = await CreateHandler(authScope.ServiceProvider, context);

    var result = await handler.AuthenticateAsync();

    Assert.False(result.Succeeded);
    Assert.NotNull(result.Failure);
    Assert.Equal("Invalid service account credential", result.Failure!.Message);
  }

  [Fact]
  public async Task HandleAuthenticateAsync_WithEmptyHeader_ShouldReturnNoResult()
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
  public async Task HandleAuthenticateAsync_WithExpiredCredential_ShouldFail()
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
        ServiceAccountAccessMode.Unrestricted, TestContext.Current.CancellationToken);
      Assert.True(createResult.IsSuccess);

      var credResult = await serviceAccountManager.AddCredentialForServer(
        createResult.Value.Id,
        "Test Credential",
        expiresAt: null,
        TestActors.User(),
        TestContext.Current.CancellationToken);
      Assert.True(credResult.IsSuccess);

      var credentialId = credResult.Value.Credential.Id;
      plainTextSecretKey = credResult.Value.PlainTextSecretKey;

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
  public async Task HandleAuthenticateAsync_WithInvalidApiKey_ShouldFail()
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
  public async Task HandleAuthenticateAsync_WithMissingHeader_ShouldReturnNoResult()
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
  public async Task HandleAuthenticateAsync_WithRevokedCredential_ShouldFail()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var serviceAccountManager = services.GetRequiredService<IServiceAccountManager>();

    var createResult = await serviceAccountManager.CreateForServer(
      "Revocation Test SA",
      null,
      ServiceAccountAccessMode.Unrestricted, TestContext.Current.CancellationToken);
    Assert.True(createResult.IsSuccess);

    var credResult = await serviceAccountManager.AddCredentialForServer(
      createResult.Value.Id,
      "Test Credential",
      expiresAt: null,
      TestActors.User(),
      TestContext.Current.CancellationToken);
    Assert.True(credResult.IsSuccess);

    var accountId = createResult.Value.Id;
    var credentialId = credResult.Value.Credential.Id;
    await serviceAccountManager.RevokeCredentialForServer(
      accountId,
      credentialId,
      TestActors.User(),
      TestContext.Current.CancellationToken);

    var apiKey = credResult.Value.PlainTextSecretKey;
    var context = CreateHttpContext(apiKey);
    var handler = await CreateHandler(services, context);

    var result = await handler.AuthenticateAsync();

    Assert.False(result.Succeeded);
  }

  [Fact]
  public async Task HandleAuthenticateAsync_WithValidApiKey_ShouldSucceed()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var serviceAccountManager = services.GetRequiredService<IServiceAccountManager>();

    var createResult = await serviceAccountManager.CreateForServer(
      "AuthHandlerTest SA",
      null,
      ServiceAccountAccessMode.Unrestricted, TestContext.Current.CancellationToken);
    Assert.True(createResult.IsSuccess);

    var credResult = await serviceAccountManager.AddCredentialForServer(
      createResult.Value.Id,
      "Test Credential",
      expiresAt: null,
      TestActors.User(),
      TestContext.Current.CancellationToken);
    Assert.True(credResult.IsSuccess);

    var plainTextSecretKey = credResult.Value.PlainTextSecretKey;

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
      PrincipalClaimValues.ServerServiceAccount,
      result.Principal.FindFirst(PrincipalClaimTypes.PrincipalType)?.Value);
    Assert.NotNull(result.Principal.FindFirst(PrincipalClaimTypes.PrincipalId)?.Value);
    Assert.NotNull(result.Principal.FindFirst(PrincipalClaimTypes.CredentialId)?.Value);
    Assert.Equal(
      PrincipalClaimValues.ServiceAccountCredentialMethod,
      result.Principal.FindFirst(UserClaimTypes.AuthenticationMethod)?.Value);

    Assert.Null(result.Principal.FindFirst(UserClaimTypes.TenantId));
    Assert.Null(result.Principal.FindFirst(UserClaimTypes.UserId));

    Assert.True(result.Principal.IsServerPrincipal());
  }

  [Fact]
  public async Task RevokeCredential_ImmediatelyInvalidatesCachedAuth()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var serviceAccountManager = services.GetRequiredService<IServiceAccountManager>();

    var createResult = await serviceAccountManager.CreateForServer(
      "Revoke Cache SA",
      null,
      ServiceAccountAccessMode.Unrestricted, TestContext.Current.CancellationToken);
    Assert.True(createResult.IsSuccess);

    var credResult = await serviceAccountManager.AddCredentialForServer(
      createResult.Value.Id,
      "Test Credential",
      expiresAt: null,
      TestActors.User(),
      TestContext.Current.CancellationToken);
    Assert.True(credResult.IsSuccess);

    var plainTextSecretKey = credResult.Value.PlainTextSecretKey;

    // Prime the cache with a successful auth.
    var primeContext = CreateHttpContext(plainTextSecretKey);
    var primeHandler = await CreateHandler(services, primeContext);
    var primeResult = await primeHandler.AuthenticateAsync();
    Assert.True(primeResult.Succeeded);

    // Revoke the credential while the auth is still cached.
    var revokeResult = await serviceAccountManager.RevokeCredentialForServer(
      createResult.Value.Id,
      credResult.Value.Credential.Id,
      TestActors.User(),
      TestContext.Current.CancellationToken);
    Assert.True(revokeResult.IsSuccess);

    var context = CreateHttpContext(plainTextSecretKey);
    var handler = await CreateHandler(services, context);
    var result = await handler.AuthenticateAsync();

    Assert.False(result.Succeeded);
  }

  [Fact]
  public async Task UpdateForServer_WhenDisablingAccount_ImmediatelyInvalidatesCachedAuth()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var serviceAccountManager = services.GetRequiredService<IServiceAccountManager>();

    var createResult = await serviceAccountManager.CreateForServer(
      "Disable Cache SA",
      null,
      ServiceAccountAccessMode.Unrestricted, TestContext.Current.CancellationToken);
    Assert.True(createResult.IsSuccess);

    var credResult = await serviceAccountManager.AddCredentialForServer(
      createResult.Value.Id,
      "Test Credential",
      expiresAt: null,
      TestActors.User(),
      TestContext.Current.CancellationToken);
    Assert.True(credResult.IsSuccess);

    var plainTextSecretKey = credResult.Value.PlainTextSecretKey;

    // Prime the cache with a successful auth.
    var primeContext = CreateHttpContext(plainTextSecretKey);
    var primeHandler = await CreateHandler(services, primeContext);
    var primeResult = await primeHandler.AuthenticateAsync();
    Assert.True(primeResult.Succeeded);

    // Disable the account via Update while the auth is still cached.
    var updateResult = await serviceAccountManager.UpdateForServer(
      createResult.Value.Id,
      "Disable Cache SA",
      null,
      isEnabled: false,
      TestActors.User(),
      TestContext.Current.CancellationToken);
    Assert.True(updateResult.IsSuccess);
    Assert.False(updateResult.Value.IsEnabled);

    var context = CreateHttpContext(plainTextSecretKey);
    var handler = await CreateHandler(services, context);
    var result = await handler.AuthenticateAsync();

    Assert.False(result.Succeeded);
  }

  [Fact]
  public async Task UpdateForTenant_WhenDisablingAccount_ImmediatelyInvalidatesCachedAuth()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var serviceAccountManager = services.GetRequiredService<IServiceAccountManager>();

    var createResult = await serviceAccountManager.CreateForTenant(
      "Tenant Disable Cache SA",
      null,
      tenant.Id,
      TestActors.User(),
      TestContext.Current.CancellationToken);
    Assert.True(createResult.IsSuccess);

    var credResult = await serviceAccountManager.AddCredentialForTenant(
      createResult.Value.Id,
      tenant.Id,
      "Test Credential",
      expiresAt: null,
      TestActors.User(),
      TestContext.Current.CancellationToken);
    Assert.True(credResult.IsSuccess);

    var plainTextSecretKey = credResult.Value.PlainTextSecretKey;

    // Prime the cache with a successful auth.
    var primeContext = CreateHttpContext(plainTextSecretKey);
    var primeHandler = await CreateHandler(services, primeContext);
    var primeResult = await primeHandler.AuthenticateAsync();
    Assert.True(primeResult.Succeeded);

    var updateResult = await serviceAccountManager.UpdateForTenant(
      createResult.Value.Id,
      tenant.Id,
      "Tenant Disable Cache SA",
      null,
      isEnabled: false,
      TestActors.User(),
      TestContext.Current.CancellationToken);
    Assert.True(updateResult.IsSuccess);
    Assert.False(updateResult.Value.IsEnabled);

    var context = CreateHttpContext(plainTextSecretKey);
    var handler = await CreateHandler(services, context);
    var result = await handler.AuthenticateAsync();

    Assert.False(result.Succeeded);
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
    var appOptions = services.GetRequiredService<IOptionsMonitor<AppOptions>>();
    var loggerFactory = services.GetRequiredService<ILoggerFactory>();
    var serviceAccountManager = services.GetRequiredService<IServiceAccountManager>();

    var scheme = new AuthenticationScheme(
      ServiceAccountCredentialAuthenticationSchemeOptions.DefaultScheme,
      ServiceAccountCredentialAuthenticationSchemeOptions.DefaultScheme,
      typeof(ServiceAccountCredentialAuthenticationHandler));

    var memoryCache = services.GetRequiredService<IMemoryCache>();

    var handler = new ServiceAccountCredentialAuthenticationHandler(
      UrlEncoder.Default,
      memoryCache,
      serviceAccountManager,
      loggerFactory,
      options,
      appOptions,
      loggerFactory.CreateLogger<ServiceAccountCredentialAuthenticationHandler>());

    await handler.InitializeAsync(scheme, context);

    return handler;
  }
}
