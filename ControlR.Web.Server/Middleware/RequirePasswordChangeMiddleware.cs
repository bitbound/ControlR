using ControlR.Libraries.Api.Contracts.Constants;

namespace ControlR.Web.Server.Middleware;

public class RequirePasswordChangeMiddleware(RequestDelegate next)
{
  private static readonly HashSet<string> _allowedApiPaths =
  [
    $"{HttpConstants.AuthEndpoint}/change-password",
    $"{HttpConstants.AuthEndpoint}/change-password-with-credentials",
    $"{HttpConstants.AuthEndpoint}/complete-password-reset",
    $"{HttpConstants.AuthEndpoint}/interactive-login",
    $"{HttpConstants.AuthEndpoint}/logout",
  ];
  private static readonly HashSet<string> _allowedPathStartSegments =
  [
    "/password-change-required",
    "/Account/Manage/ChangePassword",
    "/Account/Manage/SetPassword",
    "/Account/Logout",
    "/_framework",
    "/_content",
    "/lib",
    "/images",
    "/health"
  ];
  private static readonly PathString _changePasswordPath = new("/password-change-required");
  private static readonly HashSet<string> _staticAssetExtensions =
  [
    ".ico", 
    ".css", 
    ".js", 
    ".png", 
    ".jpg", 
    ".jpeg", 
    ".gif", 
    ".svg", 
    ".webp", 
    ".avif", 
    ".woff", 
    ".woff2", 
    ".ttf", 
    ".eot", 
    ".manifest", 
    ".webmanifest",
    ".json"
  ];

  private readonly RequestDelegate _next = next;

  public async Task Invoke(HttpContext context, UserManager<AppUser> userManager)
  {
    var identity = context.User.Identity;
    if (identity is null || !identity.IsAuthenticated)
    {
      await _next(context);
      return;
    }

    if (ShouldBypass(context.Request.Path))
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

    if (!isCookieAuthenticated || context.WebSockets.IsWebSocketRequest)
    {
      context.Response.StatusCode = StatusCodes.Status403Forbidden;
      await context.Response.WriteAsJsonAsync(new { error = "Password change required." });
      return;
    }

    context.Response.Redirect(_changePasswordPath);
  }

  private static bool ShouldBypass(PathString requestPath)
  {
    var path = requestPath.Value;
    return path is not null &&
      (
        _allowedPathStartSegments.Any(p => path.StartsWith(p, StringComparison.Ordinal)) ||
        _allowedApiPaths.Contains(path) ||
        _staticAssetExtensions.Any(ext => path.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
      );
  }
}