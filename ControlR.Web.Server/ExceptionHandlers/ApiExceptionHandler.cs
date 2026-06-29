using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ControlR.Web.Server.ExceptionHandlers;

public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
  public async ValueTask<bool> TryHandleAsync(
    HttpContext httpContext,
    Exception exception,
    CancellationToken cancellationToken)
  {
    if (!httpContext.Request.Path.StartsWithSegments("/api") &&
        !httpContext.Request.Headers.Accept.ToString().Contains("application/json", StringComparison.OrdinalIgnoreCase))
    {
      return false;
    }

    logger.LogError(exception, "Unhandled exception processing {Path}", httpContext.Request.Path);

    var problemDetails = new ProblemDetails
    {
      Status = StatusCodes.Status500InternalServerError,
      Title = "An unexpected error occurred.",
      Detail = "An unexpected error occurred.",
      Type = "https://tools.ietf.org/html/rfc9110#section-15.6.1",
    };
    problemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;

    httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
    await httpContext.Response.WriteAsJsonAsync(
      problemDetails,
      JsonSerializerOptions.Web,
      "application/problem+json",
      cancellationToken);

    return true;
  }
}
