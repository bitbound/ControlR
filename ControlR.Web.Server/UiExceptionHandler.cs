using Microsoft.AspNetCore.Diagnostics;

namespace ControlR.Web.Server;

public sealed class UiExceptionHandler(ILogger<UiExceptionHandler> logger) : IExceptionHandler
{
  public async ValueTask<bool> TryHandleAsync(
    HttpContext httpContext,
    Exception exception,
    CancellationToken cancellationToken)
  {
    if (httpContext.Request.Path.StartsWithSegments("/api") ||
        httpContext.Request.Headers.Accept.ToString().Contains("application/json", StringComparison.OrdinalIgnoreCase))
    {
      return false;
    }

    logger.LogError(exception, "Unhandled exception processing {Path}", httpContext.Request.Path);
    httpContext.Response.Redirect("/Error");
    return true;
  }
}
