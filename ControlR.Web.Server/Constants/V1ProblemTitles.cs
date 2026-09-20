using Microsoft.AspNetCore.Mvc;

namespace ControlR.Web.Server.Constants;

/// <summary>
/// The titles behind every problem response the server writes. A title names the kind of failure and is
/// the same from request to request, while the detail carries what varies. Browsers and API clients
/// should read the status code rather than the title.
/// </summary>
/// <remarks>
/// Named for V1 because V1 is the surface whose contract has to hold still, but it is registered
/// app-wide on purpose: a controller calling <see cref="ControllerBase.Problem" />, the shared
/// <see cref="Extensions.HttpResultExtensions" /> helper, and the pipeline writing a response no
/// controller ever reached must not disagree about what a 404 is called. Most titles are keyed by
/// status, because status is all the pipeline knows. A caller that knows more than the status, such as
/// one reporting a validation failure, names that failure itself. A status missing from the table keeps
/// ASP.NET's own title, because the server never answers it deliberately.
/// </remarks>
internal static class V1ProblemTitles
{
  internal const string BadGateway = "Bad gateway.";
  internal const string Conflict = "Conflict.";
  internal const string Forbidden = "Forbidden.";
  internal const string InternalServerError = "Internal server error.";
  internal const string InvalidRequest = "Invalid request.";
  internal const string NotFound = "Not found.";
  internal const string NotImplemented = "Not implemented.";

  /// <summary>
  /// RFC 9457 section 3.1: "about:blank" means the status code alone identifies the problem, which is
  /// all these responses claim. There is no problem-type registry to point a caller at.
  /// </summary>
  internal const string ProblemType = "about:blank";

  internal const string RequestEntityTooLarge = "Request entity too large.";
  internal const string RequestTimedOut = "Request timed out.";
  internal const string ServiceUnavailable = "Service unavailable.";
  internal const string TooManyRequests = "Too many requests.";
  internal const string Unauthorized = "Unauthorized.";
  internal const string ValidationFailed = "Validation failed.";

  private static readonly Dictionary<int, string> _byStatusCode = new()
  {
    [StatusCodes.Status400BadRequest] = InvalidRequest,
    [StatusCodes.Status401Unauthorized] = Unauthorized,
    [StatusCodes.Status403Forbidden] = Forbidden,
    [StatusCodes.Status404NotFound] = NotFound,
    [StatusCodes.Status408RequestTimeout] = RequestTimedOut,
    [StatusCodes.Status409Conflict] = Conflict,
    [StatusCodes.Status413RequestEntityTooLarge] = RequestEntityTooLarge,
    [StatusCodes.Status429TooManyRequests] = TooManyRequests,
    [StatusCodes.Status500InternalServerError] = InternalServerError,
    [StatusCodes.Status501NotImplemented] = NotImplemented,
    [StatusCodes.Status502BadGateway] = BadGateway,
    [StatusCodes.Status503ServiceUnavailable] = ServiceUnavailable,
  };

  /// <summary>
  /// Teaches the framework's problem writer the table, so a response the pipeline produces before a
  /// controller runs is titled exactly like one a controller produces. That covers the errors that never
  /// reach an action, such as an unmatched route, a missing credential, or a controller's empty
  /// client-error result. A status outside the table keeps ASP.NET's own title.
  /// </summary>
  internal static void ConfigureProblemDetails(ProblemDetailsOptions options)
  {
    options.CustomizeProblemDetails = context =>
    {
      var problem = context.ProblemDetails;
      if (problem is null)
      {
        return;
      }

      var statusCode = problem.Status ?? context.HttpContext.Response.StatusCode;
      if (_byStatusCode.TryGetValue(statusCode, out var title))
      {
        problem.Title = title;
        problem.Type = ProblemType;
      }
    };
  }

  /// <summary>
  /// The title for a status code. A status the server never answers on purpose is absent from the table,
  /// so this throws instead of letting a call site invent a title.
  /// </summary>
  internal static string ForStatusCode(int statusCode)
  {
    return _byStatusCode.TryGetValue(statusCode, out var title)
      ? title
      : throw new ArgumentOutOfRangeException(
        nameof(statusCode),
        statusCode,
        "No problem title is registered for this status code.");
  }
}
