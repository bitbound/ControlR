using ControlR.Web.Server.Authn;
using ControlR.Web.Server.Data;
using ControlR.Web.Server.Services;
using ControlR.Web.Server.Tests.Helpers;
using ControlR.Web.Client.Authz;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Xunit.Abstractions;
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

    var createRequest = new Libraries.Shared.Dtos.ServerApi.CreatePersonalAccessTokenRequestDto("Test Key");
    var createResult = await patManager.CreateToken(createRequest, tenantId, serverAdmin.Id);
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

    // First user should be in all roles.
    Assert.True(result.Principal.IsInRole(RoleNames.ServerAdministrator));
    Assert.True(result.Principal.IsInRole(RoleNames.TenantAdministrator));
    Assert.True(result.Principal.IsInRole(RoleNames.DeviceSuperUser));
    Assert.True(result.Principal.IsInRole(RoleNames.AgentInstaller));
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

    var createRequest = new Libraries.Shared.Dtos.ServerApi.CreatePersonalAccessTokenRequestDto("Test Key");
    var createResult = await personalAccessTokenManager.CreateToken(createRequest, tenantId, user.Id);
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

    // First user should be in all roles.
    Assert.True(result.Principal.IsInRole(RoleNames.ServerAdministrator));
    Assert.True(result.Principal.IsInRole(RoleNames.TenantAdministrator));
    Assert.True(result.Principal.IsInRole(RoleNames.DeviceSuperUser));
    Assert.True(result.Principal.IsInRole(RoleNames.AgentInstaller));

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

    var createRequest = new Libraries.Shared.Dtos.ServerApi.CreatePersonalAccessTokenRequestDto("Test Key");
    var createResult = await patManager.CreateToken(createRequest, tenant.Id, user.Id);
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
      .FirstOrDefaultAsync(x => x.Id == createResult.Value!.PersonalAccessToken.Id);

    Assert.NotNull(storedToken);
    Assert.NotNull(storedToken.LastUsed);
    Assert.Equal(expectedLastUsed, storedToken.LastUsed);
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
      roles: [RoleNames.DeviceSuperUser, RoleNames.AgentInstaller]);

    var patManager = services.GetRequiredService<IPersonalAccessTokenManager>();

    var createRequest = new Libraries.Shared.Dtos.ServerApi.CreatePersonalAccessTokenRequestDto("Test Key");
    var createResult = await patManager.CreateToken(createRequest, tenantId, normalUser.Id);
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

    // New user should only have the roles specified.
    Assert.False(result.Principal.IsInRole(RoleNames.ServerAdministrator));
    Assert.False(result.Principal.IsInRole(RoleNames.TenantAdministrator));
    Assert.True(result.Principal.IsInRole(RoleNames.DeviceSuperUser));
    Assert.True(result.Principal.IsInRole(RoleNames.AgentInstaller));
  }

  private static DefaultHttpContext CreateHttpContext(string? token)
  {
    var context = new DefaultHttpContext();

    if (!string.IsNullOrEmpty(token))
    {
      // The handler expects a personal access token header
      context.Request.Headers[PersonalAccessTokenAuthenticationSchemeOptions.DefaultHeaderName] = token;
    }

    return context;
  }

  private async Task<PersonalAccessTokenAuthenticationHandler> CreateHandler(IServiceProvider services, HttpContext context)
  {
    var options = services.GetRequiredService<IOptionsMonitor<PersonalAccessTokenAuthenticationSchemeOptions>>();
    var loggerFactory = services.GetRequiredService<ILoggerFactory>();
    var personalAccessTokenManager = services.GetRequiredService<IPersonalAccessTokenManager>();
    var userManager = services.GetRequiredService<UserManager<AppUser>>();

    var scheme = new AuthenticationScheme(
      PersonalAccessTokenAuthenticationSchemeOptions.DefaultScheme,
      PersonalAccessTokenAuthenticationSchemeOptions.DefaultScheme,
      typeof(PersonalAccessTokenAuthenticationHandler));

    var handler = new PersonalAccessTokenAuthenticationHandler(
      UrlEncoder.Default,
      userManager,
      loggerFactory,
      personalAccessTokenManager,
      options);

    await handler.InitializeAsync(scheme, context);

    return handler;
  }
}
