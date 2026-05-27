using System.Security.Claims;
using System.Text;
using ControlR.Web.Server.Data.Entities;
using ControlR.Web.Server.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Moq;

namespace ControlR.Web.Server.Tests;

public class RequirePasswordChangeMiddlewareTests
{
  [Fact]
  public async Task Invoke_WhenAllowedApiPath_ReachesNextMiddleware()
  {
    var nextCalled = false;
    var middleware = new RequirePasswordChangeMiddleware(_ =>
    {
      nextCalled = true;
      return Task.CompletedTask;
    });

    var context = CreateContext("/api/auth/change-password", IdentityConstants.ApplicationScheme);
    var userManager = CreateUserManager(new AppUser { RequirePasswordChange = true });

    await middleware.Invoke(context, userManager.Object);

    Assert.True(nextCalled);
  }

  [Fact]
  public async Task Invoke_WhenCredentialChangeApiPath_ReachesNextMiddleware()
  {
    var nextCalled = false;
    var middleware = new RequirePasswordChangeMiddleware(_ =>
    {
      nextCalled = true;
      return Task.CompletedTask;
    });

    var context = CreateContext("/api/auth/change-password-with-credentials", IdentityConstants.ApplicationScheme);
    var userManager = CreateUserManager(new AppUser { RequirePasswordChange = true });

    await middleware.Invoke(context, userManager.Object);

    Assert.True(nextCalled);
  }

  [Fact]
  public async Task Invoke_WhenCompleteResetApiPath_ReachesNextMiddleware()
  {
    var nextCalled = false;
    var middleware = new RequirePasswordChangeMiddleware(_ =>
    {
      nextCalled = true;
      return Task.CompletedTask;
    });

    var context = CreateContext("/api/auth/complete-password-reset", IdentityConstants.ApplicationScheme);
    var userManager = CreateUserManager(new AppUser { RequirePasswordChange = true });

    await middleware.Invoke(context, userManager.Object);

    Assert.True(nextCalled);
  }

  [Fact]
  public async Task Invoke_WhenApiRequestRequiresPasswordChange_ReturnsForbiddenJson()
  {
    var nextCalled = false;
    var middleware = new RequirePasswordChangeMiddleware(_ =>
    {
      nextCalled = true;
      return Task.CompletedTask;
    });

    var context = CreateContext("/api/personal-access-tokens", IdentityConstants.ApplicationScheme);
    context.Response.Body = new MemoryStream();
    var userManager = CreateUserManager(new AppUser { RequirePasswordChange = true });

    await middleware.Invoke(context, userManager.Object);

    Assert.False(nextCalled);
    Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    context.Response.Body.Position = 0;
    var payload = Encoding.UTF8.GetString(((MemoryStream)context.Response.Body).ToArray());
    Assert.Contains("Password change required", payload, StringComparison.Ordinal);
  }

  [Fact]
  public async Task Invoke_WhenCookieAuthenticatedAndPasswordChangeRequired_RedirectsToChangePassword()
  {
    var nextCalled = false;
    var middleware = new RequirePasswordChangeMiddleware(_ =>
    {
      nextCalled = true;
      return Task.CompletedTask;
    });

    var context = CreateContext("/dashboard", IdentityConstants.ApplicationScheme);
    var userManager = CreateUserManager(new AppUser { RequirePasswordChange = true });

    await middleware.Invoke(context, userManager.Object);

    Assert.False(nextCalled);
    Assert.Equal(StatusCodes.Status302Found, context.Response.StatusCode);
    Assert.Equal("/password-change-required", context.Response.Headers.Location.ToString());
  }

  [Fact]
  public async Task Invoke_WhenWebSocketRequestAndPasswordChangeRequired_ReturnsForbidden()
  {
    var nextCalled = false;
    var middleware = new RequirePasswordChangeMiddleware(_ =>
    {
      nextCalled = true;
      return Task.CompletedTask;
    });

    var context = CreateContext("/hubs/viewer", IdentityConstants.ApplicationScheme);
    context.Response.Body = new MemoryStream();
    var wsFeature = new Mock<IHttpWebSocketFeature>();
    wsFeature.Setup(f => f.IsWebSocketRequest).Returns(true);
    context.Features.Set(wsFeature.Object);
    var userManager = CreateUserManager(new AppUser { RequirePasswordChange = true });

    await middleware.Invoke(context, userManager.Object);

    Assert.False(nextCalled);
    Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    context.Response.Body.Position = 0;
    var payload = Encoding.UTF8.GetString(((MemoryStream)context.Response.Body).ToArray());
    Assert.Contains("Password change required", payload, StringComparison.Ordinal);
  }

  [Fact]
  public async Task Invoke_WhenNonCookieAuthenticatedAndPasswordChangeRequired_ReturnsForbidden()
  {
    var nextCalled = false;
    var middleware = new RequirePasswordChangeMiddleware(_ =>
    {
      nextCalled = true;
      return Task.CompletedTask;
    });

    var context = CreateContext("/dashboard", IdentityConstants.BearerScheme);
    var userManager = CreateUserManager(new AppUser { RequirePasswordChange = true });

    await middleware.Invoke(context, userManager.Object);

    Assert.False(nextCalled);
    Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
  }

  private static DefaultHttpContext CreateContext(string path, string authenticationType)
  {
    var context = new DefaultHttpContext();
    context.Request.Path = path;
    context.User = new ClaimsPrincipal(new ClaimsIdentity(
      [new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())],
      authenticationType));
    return context;
  }

  private static Mock<UserManager<AppUser>> CreateUserManager(AppUser user)
  {
    var userStore = new Mock<IUserStore<AppUser>>();
    var userManager = new Mock<UserManager<AppUser>>(
      userStore.Object,
      null!,
      null!,
      null!,
      null!,
      null!,
      null!,
      null!,
      null!);

    userManager
      .Setup(x => x.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
      .ReturnsAsync(user);

    return userManager;
  }
}