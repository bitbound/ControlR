using ControlR.Web.Server.Authn;
using ControlR.Web.Server.Data;
using ControlR.Web.Server.Constants;
using ControlR.Web.Server.Services;
using ControlR.Web.Server.Tests.Helpers;
using ControlR.Web.Server.Authz.Permissions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Identity;
using ControlR.Web.Server.Data.Entities;

namespace ControlR.Web.Server.Tests;

public class PersonalAccessTokenAuthenticationHandlerTests(ITestOutputHelper testOutput)
{
  private readonly ITestOutputHelper _testOutputHelper = testOutput;

  [Fact]
  public async Task HandleAdminAuthenticateAsync_ShouldSucceed_WithValidPersonalAccessToken()
  {
    // Arrange
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var serverAdmin = await services.CreateTestUser("admin@example.com");
    var tenantId = serverAdmin.TenantId;

    var patManager = services.GetRequiredService<IPersonalAccessTokenManager>();

    var createRequest = new InternalDtos.CreatePersonalAccessTokenRequestDto("Test Key", PersonalAccessTokenPermissionMode.InheritOwner);
    var createResult = await patManager.CreateToken(createRequest, serverAdmin.Id, new PrincipalDescriptor(PrincipalType.User, serverAdmin.Id, tenantId, "test"));
    var plainTextToken = createResult.Value!.PlainTextToken;

    var context = CreateHttpContext(plainTextToken);
    var handler = await CreateHandler(services, context);

    // Act
    var result = await handler.AuthenticateAsync();

    // Assert
    Assert.True(result.Succeeded);
    Assert.NotNull(result.Principal);
    Assert.Equal(PersonalAccessTokenAuthenticationSchemeOptions.DefaultScheme, result.Principal.Identity?.AuthenticationType);
    Assert.True(result.Principal.Identity?.IsAuthenticated);

    // Check claims
    var tenantClaim = result.Principal.FindFirst(UserClaimTypes.TenantId);
    Assert.NotNull(tenantClaim);
    Assert.Equal(tenantId.ToString(), tenantClaim.Value);
  }

  [Fact]
  public async Task HandleAuthenticateAsync_LockedOutUser_ReturnsFail()
  {
    // Arrange — lockout propagation through the PAT auth pipeline.
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;

    var tenant = await services.CreateTestTenant();
    var user = await services.CreateTestUser(tenant.Id);
    var patManager = services.GetRequiredService<IPersonalAccessTokenManager>();

    var createRequest = new InternalDtos.CreatePersonalAccessTokenRequestDto("Test Key", PersonalAccessTokenPermissionMode.InheritOwner);
    var createResult = await patManager.CreateToken(createRequest, user.Id, new PrincipalDescriptor(PrincipalType.User, user.Id, user.TenantId, "test"));
    var plainTextToken = createResult.Value!.PlainTextToken;

    // Lock the user (re-fetch so EF tracks the instance correctly).
    var userManager = services.GetRequiredService<UserManager<AppUser>>();
    var trackedUser = await userManager.FindByIdAsync(user.Id.ToString());
    Assert.NotNull(trackedUser);
    await userManager.SetLockoutEnabledAsync(trackedUser, true);
    await userManager.SetLockoutEndDateAsync(trackedUser, DateTimeOffset.UtcNow.AddHours(1));

    var context = CreateHttpContext(plainTextToken);
    var handler = await CreateHandler(services, context);

    // Act
    var result = await handler.AuthenticateAsync();

    // Assert
    Assert.False(result.Succeeded);
    Assert.NotNull(result.Failure);
    Assert.Equal("User account is locked", result.Failure.Message);
  }

  [Fact]
  public async Task HandleAuthenticateAsync_MalformedTokenPrefixes_CollapseToSingleCacheKey()
  {
    // Distinct malformed prefixes must all collapse to the single fixed "invalid" token key,
    // so an attacker cannot plant unbounded cache entries.
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var memoryCache = services.GetRequiredService<IMemoryCache>();

    var malformedTokens = new[] { ":foo", "bar:secret", "baz:secret", "qux:secret" };

    for (var i = 0; i < malformedTokens.Length; i++)
    {
      var context = CreateHttpContext(malformedTokens[i]);
      // Distinct IP per attempt so the IP-axis throttle does not fire.
      context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse($"10.0.0.{i + 1}");
      var handler = await CreateHandler(services, context);
      var result = await handler.AuthenticateAsync();
      Assert.False(result.Succeeded);
    }

    var fixedKey = CacheKeys.GetPersonalAccessTokenAuthFailureByToken("invalid");
    Assert.True(memoryCache.TryGetValue<int>(fixedKey, out var failures));
    Assert.Equal(malformedTokens.Length, failures);
  }

  [Fact]
  public async Task HandleAuthenticateAsync_ShouldCreateCorrectIdentity()
  {
    // Arrange
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var user = await services.CreateTestUser();
    var tenantId = user.TenantId;
    var personalAccessTokenManager = services.GetRequiredService<IPersonalAccessTokenManager>();
    var userManager = services.GetRequiredService<UserManager<AppUser>>();

    var createRequest = new InternalDtos.CreatePersonalAccessTokenRequestDto("Test Key", PersonalAccessTokenPermissionMode.InheritOwner);
    var createResult = await personalAccessTokenManager.CreateToken(createRequest, user.Id, new PrincipalDescriptor(PrincipalType.User, user.Id, tenantId, "test"));
    var plainTextToken = createResult.Value!.PlainTextToken;

    var context = CreateHttpContext(plainTextToken);
    var handler = await CreateHandler(services, context);

    // Act
    var result = await handler.AuthenticateAsync();

    // Assert
    Assert.True(result.Succeeded);
    Assert.NotNull(result.Principal);

    var identity = result.Principal.Identity as ClaimsIdentity;
    Assert.NotNull(identity);
    Assert.Equal(PersonalAccessTokenAuthenticationSchemeOptions.DefaultScheme, identity.AuthenticationType);
    Assert.True(identity.IsAuthenticated);

    // Assert UserManager<T> works with the resulting principal.
    var identityUser = await userManager.GetUserAsync(result.Principal);
    Assert.NotNull(identityUser);
    Assert.Equal(user.Id, identityUser.Id);
  }

  [Fact]
  public async Task HandleAuthenticateAsync_ShouldFail_WithInvalidPersonalAccessToken()
  {
    // Arrange
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    using var scope = testApp.CreateScope();
    var context = CreateHttpContext("invalid-token");
    var handler = await CreateHandler(scope.ServiceProvider, context);

    // Act
    var result = await handler.AuthenticateAsync();

    // Assert
    Assert.False(result.Succeeded);
    Assert.Equal("Invalid personal access token", result.Failure?.Message);
  }

  [Fact]
  public async Task HandleAuthenticateAsync_ShouldReturnNoResult_WithEmptyToken()
  {
    // Arrange
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    using var scope = testApp.CreateScope();
    var context = CreateHttpContext("");
    var handler = await CreateHandler(scope.ServiceProvider, context);

    // Act
    var result = await handler.AuthenticateAsync();

    // Assert
    Assert.False(result.Succeeded);
    Assert.Null(result.Failure);
  }

  [Fact]
  public async Task HandleAuthenticateAsync_ShouldReturnNoResult_WithMissingToken()
  {
    // Arrange
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    using var scope = testApp.CreateScope();
    var context = CreateHttpContext(null);
    var handler = await CreateHandler(scope.ServiceProvider, context);

    // Act
    var result = await handler.AuthenticateAsync();

    // Assert
    Assert.False(result.Succeeded);
    Assert.Null(result.Failure);
  }

  [Fact]
  public async Task HandleAuthenticateAsync_ShouldReturnNoResult_WithWhitespaceToken()
  {
    // Arrange
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    using var scope = testApp.CreateScope();
    var context = CreateHttpContext("   ");
    var handler = await CreateHandler(scope.ServiceProvider, context);

    // Act
    var result = await handler.AuthenticateAsync();

    // Assert
    Assert.False(result.Succeeded);
    Assert.Null(result.Failure);
  }

  [Fact]
  public async Task HandleAuthenticateAsync_ShouldUpdateLastUsed_OnSuccessfulAuth()
  {
    // Arrange
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var tenant = await services.CreateTestTenant();
    var user = await services.CreateTestUser(tenant.Id);
    var patManager = services.GetRequiredService<IPersonalAccessTokenManager>();
    var timeProvider = testApp.TimeProvider;
    await using var db = services.GetRequiredService<AppDb>();

    var createRequest = new InternalDtos.CreatePersonalAccessTokenRequestDto("Test Key", PersonalAccessTokenPermissionMode.InheritOwner);
    var createResult = await patManager.CreateToken(createRequest, user.Id, new PrincipalDescriptor(PrincipalType.User, user.Id, user.TenantId, "test"));
    var plainTextToken = createResult.Value!.PlainTextToken;

    // Advance time
    timeProvider.Advance(TimeSpan.FromHours(1));
    var expectedLastUsed = timeProvider.GetUtcNow();

    var context = CreateHttpContext(plainTextToken);
    var handler = await CreateHandler(services, context);

    // Act
    await handler.AuthenticateAsync();

    // Assert - LastUsed should be updated
    var storedToken = await db.PersonalAccessTokens
      .IgnoreQueryFilters()
      .FirstOrDefaultAsync(x => x.Id == createResult.Value!.PersonalAccessToken.Id, TestContext.Current.CancellationToken);

    Assert.NotNull(storedToken);
    Assert.NotNull(storedToken.LastUsed);
    Assert.InRange(
      storedToken.LastUsed.Value,
      expectedLastUsed.AddMilliseconds(-5),
      expectedLastUsed.AddMilliseconds(5));
  }

  [Fact]
  public async Task HandleAuthenticateAsync_TooManyFailures_ThrottlesByTokenId()
  {
    // Arrange — throttle after MaxFailures (5) bad attempts within the failure window.
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;

    // Use a token id prefix that won't match any real token, so all attempts fail validation.
    const string badTokenPrefix = "badprefix:secret";

    // Act — 5 failed attempts, then a 6th should be throttled.
    for (var i = 0; i < 5; i++)
    {
      var failedContext = CreateHttpContext(badTokenPrefix);
      var failedHandler = await CreateHandler(services, failedContext);
      var failedResult = await failedHandler.AuthenticateAsync();
      Assert.False(failedResult.Succeeded);
      Assert.Equal("Invalid personal access token", failedResult.Failure?.Message);
    }

    var throttledContext = CreateHttpContext(badTokenPrefix);
    var throttledHandler = await CreateHandler(services, throttledContext);
    var throttledResult = await throttledHandler.AuthenticateAsync();

    // Assert — the 6th call must hit the throttle branch, not the validation branch.
    // The handler checks the IP-based throttle first; from a single source IP after 5
    // bad attempts, the IP-axis throttle fires (the token-axis throttle would fire
    // from a fresh IP after 5 token-id-specific failures).
    Assert.False(throttledResult.Succeeded);
    Assert.NotNull(throttledResult.Failure);
    Assert.Equal(
      "Too many failed token attempts from this source. Try again later.",
      throttledResult.Failure.Message);
  }

  [Fact]
  public async Task HandleAuthenticateAsync_ValidToken_EmitsCanonicalPrincipalAndCredentialClaims()
  {
    // Arrange — every claim the PermissionEvaluator reads must be emitted.
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;

    var tenant = await services.CreateTestTenant();
    var user = await services.CreateTestUser(tenant.Id);
    var patManager = services.GetRequiredService<IPersonalAccessTokenManager>();

    var createRequest = new InternalDtos.CreatePersonalAccessTokenRequestDto("Canonical Claim Test", PersonalAccessTokenPermissionMode.InheritOwner);
    var createResult = await patManager.CreateToken(createRequest, user.Id, new PrincipalDescriptor(PrincipalType.User, user.Id, user.TenantId, "test"));
    Assert.True(createResult.IsSuccess);
    var plainTextToken = createResult.Value!.PlainTextToken;

    var context = CreateHttpContext(plainTextToken);
    var handler = await CreateHandler(services, context);

    // Act
    var result = await handler.AuthenticateAsync();

    // Assert
    Assert.True(result.Succeeded);
    Assert.NotNull(result.Principal);

    // Canonical identity claims
    var tenantClaim = result.Principal.FindFirst(UserClaimTypes.TenantId);
    Assert.NotNull(tenantClaim);
    Assert.Equal(tenant.Id.ToString(), tenantClaim.Value);

    var userIdClaim = result.Principal.FindFirst(UserClaimTypes.UserId);
    Assert.NotNull(userIdClaim);
    Assert.Equal(user.Id.ToString(), userIdClaim.Value);

    var authMethodClaim = result.Principal.FindFirst(UserClaimTypes.AuthenticationMethod);
    Assert.NotNull(authMethodClaim);
    Assert.Equal(PrincipalClaimValues.PersonalAccessTokenMethod, authMethodClaim.Value);

    // Principal descriptor claims (consumed by PermissionEvaluator)
    var principalTypeClaim = result.Principal.FindFirst(PrincipalClaimTypes.PrincipalType);
    Assert.NotNull(principalTypeClaim);
    Assert.Equal(PrincipalClaimValues.User, principalTypeClaim.Value);

    var principalIdClaim = result.Principal.FindFirst(PrincipalClaimTypes.PrincipalId);
    Assert.NotNull(principalIdClaim);
    Assert.Equal(user.Id.ToString(), principalIdClaim.Value);

    var credentialIdClaim = result.Principal.FindFirst(PrincipalClaimTypes.CredentialId);
    Assert.NotNull(credentialIdClaim);
    Assert.Equal(createResult.Value.PersonalAccessToken.Id.ToString(), credentialIdClaim.Value);

    var credentialTypeClaim = result.Principal.FindFirst(PrincipalClaimTypes.CredentialType);
    Assert.NotNull(credentialTypeClaim);
    Assert.Equal(PrincipalClaimValues.PersonalAccessTokenCredentialType, credentialTypeClaim.Value);
  }

  [Fact]
  public async Task HandleUserAuthenticateAsync_ShouldSucceed_WithValidPersonalAccessToken()
  {
    // Arrange
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var serverAdmin = await services.CreateTestUser("admin@example.com");
    var tenantId = serverAdmin.TenantId;

    var normalUser = await services.CreateTestUser(
      tenantId: tenantId,
      presets: [PermissionPresets.DeviceSuperUser, PermissionPresets.AgentInstaller]);

    var patManager = services.GetRequiredService<IPersonalAccessTokenManager>();

    var createRequest = new InternalDtos.CreatePersonalAccessTokenRequestDto("Test Key", PersonalAccessTokenPermissionMode.InheritOwner);
    var createResult = await patManager.CreateToken(createRequest, normalUser.Id, new PrincipalDescriptor(PrincipalType.User, normalUser.Id, tenantId, "test"));
    var plainTextToken = createResult.Value!.PlainTextToken;

    var context = CreateHttpContext(plainTextToken);
    var handler = await CreateHandler(services, context);

    // Act
    var result = await handler.AuthenticateAsync();

    // Assert
    Assert.True(result.Succeeded);
    Assert.NotNull(result.Principal);
    Assert.Equal(PersonalAccessTokenAuthenticationSchemeOptions.DefaultScheme, result.Principal.Identity?.AuthenticationType);
    Assert.True(result.Principal.Identity?.IsAuthenticated);

    // Check claims
    var tenantClaim = result.Principal.FindFirst(UserClaimTypes.TenantId);
    Assert.NotNull(tenantClaim);
    Assert.Equal(tenantId.ToString(), tenantClaim.Value);
  }

  private static DefaultHttpContext CreateHttpContext(string? token, IServiceProvider? services = null)
  {
    var context = new DefaultHttpContext();

    if (!string.IsNullOrEmpty(token))
    {
      // The handler expects a personal access token header
      context.Request.Headers[PersonalAccessTokenAuthenticationSchemeOptions.DefaultHeaderName] = token;
    }

    if (services is not null)
    {
      context.RequestServices = services;
    }

    return context;
  }

  private async Task<PersonalAccessTokenAuthenticationHandler> CreateHandler(IServiceProvider services, HttpContext context)
  {
    var options = services.GetRequiredService<IOptionsMonitor<PersonalAccessTokenAuthenticationSchemeOptions>>();
    var loggerFactory = services.GetRequiredService<ILoggerFactory>();
    var personalAccessTokenManager = services.GetRequiredService<IPersonalAccessTokenManager>();
    var userManager = services.GetRequiredService<UserManager<AppUser>>();
    var timeProvider = services.GetRequiredService<TimeProvider>();
    var memoryCache = services.GetRequiredService<IMemoryCache>();

    var scheme = new AuthenticationScheme(
      PersonalAccessTokenAuthenticationSchemeOptions.DefaultScheme,
      PersonalAccessTokenAuthenticationSchemeOptions.DefaultScheme,
      typeof(PersonalAccessTokenAuthenticationHandler));

    var handler = new PersonalAccessTokenAuthenticationHandler(
      UrlEncoder.Default,
      userManager,
      loggerFactory,
      personalAccessTokenManager,
      memoryCache,
      options);

    await handler.InitializeAsync(scheme, context);

    return handler;
  }
}
