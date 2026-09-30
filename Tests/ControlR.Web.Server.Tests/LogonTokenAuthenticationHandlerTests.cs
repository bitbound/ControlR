using System.Text.Encodings.Web;
using ControlR.Web.Server.Authn;
using ControlR.Web.Server.Data.Entities;
using ControlR.Web.Server.Services.LogonTokens;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ControlR.Web.Server.Tests;

public class LogonTokenAuthenticationHandlerTests(ITestOutputHelper testOutput)
{
  private readonly ITestOutputHelper _testOutputHelper = testOutput;

  [Fact]
  public async Task HandleAuthenticateAsync_LockedOutUser_ReturnsFail()
  {
    // Arrange
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;

    var tenant = await services.CreateTestTenant();
    var user = await services.CreateTestUser(tenant.Id);
    var device = await services.CreateTestDevice(tenant.Id);

    // Create a logon token for the user
    var logonTokenProvider = services.GetRequiredService<ILogonTokenProvider>();
    var tokenResult = await logonTokenProvider.CreateToken(
      device.Id, tenant.Id, user.Id,
      cancellationToken: TestContext.Current.CancellationToken);
    Assert.True(tokenResult.IsSuccess);
    var token = tokenResult.Value!.Token;

    // Lock the user out (re-fetch via UserManager so EF tracks the instance correctly)
    var userManager = services.GetRequiredService<UserManager<AppUser>>();
    var trackedUser = await userManager.FindByIdAsync(user.Id.ToString());
    Assert.NotNull(trackedUser);
    await userManager.SetLockoutEnabledAsync(trackedUser, true);
    await userManager.SetLockoutEndDateAsync(trackedUser, DateTimeOffset.UtcNow.AddHours(1));

    var context = CreateHttpContext(services, token, device.Id);
    var handler = await CreateHandler(services, context);

    // Act
    var result = await handler.AuthenticateAsync();

    // Assert
    Assert.False(result.Succeeded);
    Assert.NotNull(result.Failure);
    Assert.Equal("User account is locked", result.Failure.Message);
  }

  [Fact]
  public async Task HandleAuthenticateAsync_ValidToken_EmitsLogonTokenMethodClaim()
  {
    // Arrange — pins the canonical claim set emitted by the logon-token handler.
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;

    var tenant = await services.CreateTestTenant();
    var user = await services.CreateTestUser(tenant.Id);
    var device = await services.CreateTestDevice(tenant.Id);

    var logonTokenProvider = services.GetRequiredService<ILogonTokenProvider>();
    var tokenResult = await logonTokenProvider.CreateToken(
      device.Id, tenant.Id, user.Id,
      cancellationToken: TestContext.Current.CancellationToken);
    Assert.True(tokenResult.IsSuccess);
    var token = tokenResult.Value!.Token;

    var context = CreateHttpContext(services, token, device.Id);
    var handler = await CreateHandler(services, context);

    // Act
    var result = await handler.AuthenticateAsync();

    // Assert
    Assert.True(result.Succeeded);
    Assert.NotNull(result.Principal);
    Assert.NotNull(result.Principal.Identity);
    Assert.True(result.Principal.Identity.IsAuthenticated);
    Assert.Equal(
      LogonTokenAuthenticationSchemeOptions.DefaultScheme,
      result.Principal.Identity.AuthenticationType);

    var tenantClaim = result.Principal.FindFirst(UserClaimTypes.TenantId);
    Assert.NotNull(tenantClaim);
    Assert.Equal(tenant.Id.ToString(), tenantClaim.Value);

    var authMethodClaim = result.Principal.FindFirst(UserClaimTypes.AuthenticationMethod);
    Assert.NotNull(authMethodClaim);
    Assert.Equal(PrincipalClaimValues.LogonTokenMethod, authMethodClaim.Value);

    var deviceSessionScopeClaim = result.Principal.FindFirst(UserClaimTypes.DeviceSessionScope);
    Assert.NotNull(deviceSessionScopeClaim);
    Assert.Equal(device.Id.ToString(), deviceSessionScopeClaim.Value);

    var principalTypeClaim = result.Principal.FindFirst(PrincipalClaimTypes.PrincipalType);
    Assert.NotNull(principalTypeClaim);
    Assert.Equal(PrincipalClaimValues.User, principalTypeClaim.Value);
  }

  [Fact]
  public async Task HandleAuthenticateAsync_ValidToken_SignsInCookieBoundedBySessionExpiration()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;

    var tenant = await services.CreateTestTenant();
    var user = await services.CreateTestUser(tenant.Id);
    var device = await services.CreateTestDevice(tenant.Id);

    var logonTokenProvider = services.GetRequiredService<ILogonTokenProvider>();
    var tokenResult = await logonTokenProvider.CreateToken(
      device.Id, tenant.Id, user.Id,
      sessionExpirationMinutes: 25,
      cancellationToken: TestContext.Current.CancellationToken);
    Assert.True(tokenResult.IsSuccess);

    var context = CreateHttpContext(services, tokenResult.Value!.Token, device.Id);
    var handler = await CreateHandler(services, context);

    var result = await handler.AuthenticateAsync();

    Assert.True(result.Succeeded);

    // Redemption must mint the application cookie directly from token claims: the requested
    // session duration as a fixed deadline, with refresh off so sliding renewal cannot
    // extend it.
    var setCookie = Assert.Single(context.Response.Headers["Set-Cookie"].ToArray());
    Assert.NotNull(setCookie);
    var cookieOptions = services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
      .Get(IdentityConstants.ApplicationScheme);
    var cookiePairs = setCookie!.Split(';')
      .Select(pair => pair.Trim())
      .Select(pair => pair.Split('=', 2))
      .Where(parts => parts.Length == 2)
      .ToDictionary(parts => parts[0], parts => parts[1], StringComparer.OrdinalIgnoreCase);

    Assert.Equal(cookieOptions.Cookie!.Name!, cookiePairs.Keys.First());
    var cookieValue = cookiePairs.Values.First();

    var ticket = cookieOptions.TicketDataFormat.Unprotect(cookieValue);
    Assert.NotNull(ticket);
    Assert.True(ticket.Properties.IsPersistent);
    Assert.False(ticket.Properties.AllowRefresh);
    // The ticket serializer truncates sub-second ticks, so compare at second precision.
    Assert.Equal(
      testApp.TimeProvider.GetUtcNow().AddMinutes(25).ToUnixTimeSeconds(),
      ticket.Properties.ExpiresUtc?.ToUnixTimeSeconds());
  }

  private static DefaultHttpContext CreateHttpContext(IServiceProvider services, string token, Guid deviceId)
  {
    var context = new DefaultHttpContext
    {
      Request =
      {
        QueryString = new QueryString($"?logonToken={Uri.EscapeDataString(token)}&deviceId={deviceId}"),
      },
      RequestServices = services,
    };
    return context;
  }

  private async Task<LogonTokenAuthenticationHandler> CreateHandler(
    IServiceProvider services,
    HttpContext context)
  {
    var options = services.GetRequiredService<IOptionsMonitor<LogonTokenAuthenticationSchemeOptions>>();
    var loggerFactory = services.GetRequiredService<ILoggerFactory>();
    var userManager = services.GetRequiredService<UserManager<AppUser>>();
    var timeProvider = services.GetRequiredService<TimeProvider>();
    var logonTokenProvider = services.GetRequiredService<ILogonTokenProvider>();

    var scheme = new AuthenticationScheme(
      LogonTokenAuthenticationSchemeOptions.DefaultScheme,
      LogonTokenAuthenticationSchemeOptions.DefaultScheme,
      typeof(LogonTokenAuthenticationHandler));

    var handler = new LogonTokenAuthenticationHandler(
      UrlEncoder.Default,
      userManager,
      timeProvider,
      options,
      loggerFactory,
      logonTokenProvider);

    await handler.InitializeAsync(scheme, context);

    return handler;
  }
}
