using ControlR.Libraries.Shared.Constants;
using ControlR.Web.Server.Authentication;
using ControlR.Web.Server.Data;
using ControlR.Web.Server.Data.Entities;
using ControlR.Web.Server.Services;
using ControlR.Web.Server.Tests.Helpers;
using ControlR.Web.Client.Authz;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Xunit.Abstractions;

namespace ControlR.Web.Server.Tests;

public class ApiKeyAuthenticationHandlerTests(ITestOutputHelper testOutput)
{
  private readonly ITestOutputHelper _testOutputHelper = testOutput;

  [Fact]
  public async Task HandleAuthenticateAsync_ShouldSucceed_WithValidApiKey()
  {
    // Arrange
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    var apiKeyManager = testApp.App.Services.GetRequiredService<IApiKeyManager>();
    using var db = testApp.App.Services.GetRequiredService<AppDb>();

    var tenant = new Tenant { Name = "Test Tenant" };
    db.Tenants.Add(tenant);
    await db.SaveChangesAsync();

    var createRequest = new ControlR.Libraries.Shared.Dtos.ServerApi.CreateApiKeyRequestDto("Test Key");
    var createResult = await apiKeyManager.CreateWithKey(createRequest, tenant.Id);
    var plainTextKey = createResult.Value!.PlainTextKey;

    var context = CreateHttpContext(plainTextKey);
    var handler = await CreateHandler(testApp, context);

    // Act
    var result = await handler.AuthenticateAsync();

    // Assert
    Assert.True(result.Succeeded);
    Assert.NotNull(result.Principal);
    Assert.Equal(ApiKeyAuthenticationSchemeOptions.DefaultScheme, result.Principal.Identity?.AuthenticationType);
    Assert.True(result.Principal.Identity?.IsAuthenticated);

    // Check claims
    var tenantClaim = result.Principal.FindFirst("TenantId");
    Assert.NotNull(tenantClaim);
    Assert.Equal(tenant.Id.ToString(), tenantClaim.Value);

    var roleClaim = result.Principal.FindFirst(ClaimTypes.Role);
    Assert.NotNull(roleClaim);
    Assert.Equal(RoleNames.TenantAdministrator, roleClaim.Value);
  }

  [Fact]
  public async Task HandleAuthenticateAsync_ShouldFail_WithInvalidApiKey()
  {
    // Arrange
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    var context = CreateHttpContext("invalid-key");
    var handler = await CreateHandler(testApp, context);

    // Act
    var result = await handler.AuthenticateAsync();

    // Assert
    Assert.False(result.Succeeded);
    Assert.Equal("Invalid API key", result.Failure?.Message);
  }

  [Fact]
  public async Task HandleAuthenticateAsync_ShouldReturnNoResult_WithMissingApiKey()
  {
    // Arrange
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    var context = CreateHttpContext(null);
    var handler = await CreateHandler(testApp, context);

    // Act
    var result = await handler.AuthenticateAsync();

    // Assert
    Assert.False(result.Succeeded);
    Assert.Null(result.Failure);
  }

  [Fact]
  public async Task HandleAuthenticateAsync_ShouldReturnNoResult_WithEmptyApiKey()
  {
    // Arrange
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    var context = CreateHttpContext("");
    var handler = await CreateHandler(testApp, context);

    // Act
    var result = await handler.AuthenticateAsync();

    // Assert
    Assert.False(result.Succeeded);
    Assert.Null(result.Failure);
  }

  [Fact]
  public async Task HandleAuthenticateAsync_ShouldReturnNoResult_WithWhitespaceApiKey()
  {
    // Arrange
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    var context = CreateHttpContext("   ");
    var handler = await CreateHandler(testApp, context);

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
    var apiKeyManager = testApp.App.Services.GetRequiredService<IApiKeyManager>();
    var timeProvider = testApp.TimeProvider;
    using var db = testApp.App.Services.GetRequiredService<AppDb>();

    var tenant = new Tenant { Name = "Test Tenant" };
    db.Tenants.Add(tenant);
    await db.SaveChangesAsync();

    var createRequest = new ControlR.Libraries.Shared.Dtos.ServerApi.CreateApiKeyRequestDto("Test Key");
    var createResult = await apiKeyManager.CreateWithKey(createRequest, tenant.Id);
    var plainTextKey = createResult.Value!.PlainTextKey;

    // Advance time
    timeProvider.Advance(TimeSpan.FromHours(1));
    var expectedLastUsed = timeProvider.GetUtcNow();

    var context = CreateHttpContext(plainTextKey);
    var handler = await CreateHandler(testApp, context);

    // Act
    await handler.AuthenticateAsync();

    // Assert - LastUsed should be updated
    var storedKey = await db.ApiKeys
      .IgnoreQueryFilters()
      .FirstOrDefaultAsync(x => x.Id == createResult.Value!.ApiKey.Id);
    
    Assert.NotNull(storedKey);
    Assert.NotNull(storedKey.LastUsed);
    Assert.Equal(expectedLastUsed, storedKey.LastUsed);
  }

  [Fact]
  public async Task HandleAuthenticateAsync_ShouldCreateCorrectIdentity()
  {
    // Arrange
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    var apiKeyManager = testApp.App.Services.GetRequiredService<IApiKeyManager>();
    using var db = testApp.App.Services.GetRequiredService<AppDb>();

    var tenant = new Tenant { Name = "Test Tenant" };
    db.Tenants.Add(tenant);
    await db.SaveChangesAsync();

    var createRequest = new ControlR.Libraries.Shared.Dtos.ServerApi.CreateApiKeyRequestDto("Test Key");
    var createResult = await apiKeyManager.CreateWithKey(createRequest, tenant.Id);
    var plainTextKey = createResult.Value!.PlainTextKey;

    var context = CreateHttpContext(plainTextKey);
    var handler = await CreateHandler(testApp, context);

    // Act
    var result = await handler.AuthenticateAsync();

    // Assert
    Assert.True(result.Succeeded);
    Assert.NotNull(result.Principal);
    
    var identity = result.Principal.Identity as ClaimsIdentity;
    Assert.NotNull(identity);
    Assert.Equal(ApiKeyAuthenticationSchemeOptions.DefaultScheme, identity.AuthenticationType);
    Assert.True(identity.IsAuthenticated);
    
    // Check all expected claims are present
    var claims = identity.Claims.ToList();
    Assert.Contains(claims, c => c.Type == "TenantId" && c.Value == tenant.Id.ToString());
    Assert.Contains(claims, c => c.Type == ClaimTypes.Role && c.Value == RoleNames.TenantAdministrator);
  }

  private async Task<ApiKeyAuthenticationHandler> CreateHandler(TestApp testApp, HttpContext context)
  {
    var options = testApp.App.Services.GetRequiredService<IOptionsMonitor<ApiKeyAuthenticationSchemeOptions>>();
    var loggerFactory = testApp.App.Services.GetRequiredService<ILoggerFactory>();
    var apiKeyManager = testApp.App.Services.GetRequiredService<IApiKeyManager>();
    
    var scheme = new AuthenticationScheme(
      ApiKeyAuthenticationSchemeOptions.DefaultScheme,
      ApiKeyAuthenticationSchemeOptions.DefaultScheme,
      typeof(ApiKeyAuthenticationHandler));

    var handler = new ApiKeyAuthenticationHandler(options, loggerFactory, UrlEncoder.Default, apiKeyManager);
    await handler.InitializeAsync(scheme, context);
    
    return handler;
  }

  private HttpContext CreateHttpContext(string? apiKey)
  {
    var context = new DefaultHttpContext();
    
    if (!string.IsNullOrEmpty(apiKey))
    {
      context.Request.Headers["x-api-key"] = apiKey;
    }
    
    return context;
  }
}
