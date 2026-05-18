using Microsoft.AspNetCore.Authentication;

namespace ControlR.Web.Server.Middleware;

public class RequirePasswordChangeMiddleware(RequestDelegate next)
{
  private static readonly HashSet<string> AllowedApiPaths =
  [
    $"{ControlR.Libraries.Api.Contracts.Constants.HttpConstants.AuthEndpoint}/change-password",
    $"{ControlR.Libraries.Api.Contracts.Constants.HttpConstants.AuthEndpoint}/logout"
  ];
  private static readonly PathString ChangePasswordPath = new("/Account/Manage/ChangePassword");
  private static readonly PathString SetPasswordPath = new("/Account/Manage/SetPassword");

  private readonly RequestDelegate _next = next;

  public async Task Invoke(HttpContext context, UserManager<AppUser> userManager)
  {
    if (!context.User.Identity?.IsAuthenticated ?? true)
    {
      await _next(context);
      return;
    }

    var user = await userManager.GetUserAsync(context.User);
    if (user is null || !user.RequirePasswordChange)
    {
      await _next(context);
      return;
    }

    if (ShouldBypass(context.Request.Path))
    {
      await _next(context);
      return;
    }

    if (context.Request.Path.StartsWithSegments("/api"))
    {
      context.Response.StatusCode = StatusCodes.Status403Forbidden;
      await context.Response.WriteAsJsonAsync(new { error = "Password change required." });
      return;
    }

    var isCookieAuthenticated = string.Equals(
      context.User.Identity?.AuthenticationType,
      IdentityConstants.ApplicationScheme,
      StringComparison.Ordinal);

    if (!isCookieAuthenticated)
    {
      context.Response.StatusCode = StatusCodes.Status403Forbidden;
      return;
    }

    context.Response.Redirect(ChangePasswordPath);
  }

  private static bool ShouldBypass(PathString requestPath)
  {
    if (requestPath.StartsWithSegments(ChangePasswordPath) ||
        requestPath.StartsWithSegments(SetPasswordPath) ||
        requestPath.StartsWithSegments("/Account/Logout") ||
        requestPath.StartsWithSegments("/_framework") ||
        requestPath.StartsWithSegments("/_content") ||
        requestPath.StartsWithSegments("/css") ||
        requestPath.StartsWithSegments("/js") ||
        requestPath.StartsWithSegments("/health"))
    {
      return true;
    }

    return requestPath.StartsWithSegments("/api") && AllowedApiPaths.Contains(requestPath.Value ?? string.Empty);
  }
}