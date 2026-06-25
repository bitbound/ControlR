using ControlR.Web.Server.Primitives;
using Microsoft.AspNetCore.Mvc;

namespace ControlR.Web.Server.Extensions;

/// <summary>
/// Extension methods for converting <see cref="HttpResult"/> to ActionResult.
/// </summary>
public static class HttpResultExtensions
{
  /// <summary>
  /// Converts an HttpResult to an ActionResult based on the error code.
  /// </summary>
  public static ActionResult ToActionResult(this HttpResult result)
  {
    if (result.IsSuccess)
    {
      return new NoContentResult();
    }

    return CreateProblemResult(result);
  }

  /// <summary>
  /// Converts an HttpResult&lt;T&gt; to an ActionResult&lt;T&gt; based on the error code.
  /// </summary>
  public static ActionResult<T> ToActionResult<T>(this HttpResult<T> result)
  {
    if (result.IsSuccess)
    {
      return new OkObjectResult(result.Value);
    }

    return CreateProblemResult(result.ToHttpResult());
  }

  private static ObjectResult CreateProblemResult(HttpResult result)
  {
    var (statusCode, title) = result.ErrorCode switch
    {
      HttpResultErrorCode.BadRequest => (StatusCodes.Status400BadRequest, "Bad Request"),
      HttpResultErrorCode.Conflict => (StatusCodes.Status409Conflict, "Conflict"),
      HttpResultErrorCode.Forbidden => (StatusCodes.Status403Forbidden, "Forbidden"),
      HttpResultErrorCode.NotFound => (StatusCodes.Status404NotFound, "Not Found"),
      HttpResultErrorCode.Unauthorized => (StatusCodes.Status401Unauthorized, "Unauthorized"),
      HttpResultErrorCode.ValidationFailed => (StatusCodes.Status400BadRequest, "Validation Failed"),
      HttpResultErrorCode.NotImplemented => (StatusCodes.Status501NotImplemented, "Not Implemented"),
      HttpResultErrorCode.ServiceUnavailable => (StatusCodes.Status503ServiceUnavailable, "Service Unavailable"),
      _ => (StatusCodes.Status500InternalServerError, "Internal Server Error")
    };

    var problem = new ProblemDetails
    {
      Status = statusCode,
      Title = title,
      Detail = result.Reason,
      Type = GetProblemType(statusCode),
    };

    if (result.Extensions is { Count: > 0 })
    {
      foreach (var kvp in result.Extensions)
      {
        problem.Extensions[kvp.Key] = kvp.Value;
      }
    }

    return new ObjectResult(problem)
    {
      StatusCode = statusCode,
    };
  }

  private static string GetProblemType(int statusCode) => statusCode switch
  {
    StatusCodes.Status400BadRequest => "https://tools.ietf.org/html/rfc9110#section-15.5.1",
    StatusCodes.Status401Unauthorized => "https://tools.ietf.org/html/rfc9110#section-15.5.2",
    StatusCodes.Status403Forbidden => "https://tools.ietf.org/html/rfc9110#section-15.5.3",
    StatusCodes.Status404NotFound => "https://tools.ietf.org/html/rfc9110#section-15.5.4",
    StatusCodes.Status409Conflict => "https://tools.ietf.org/html/rfc9110#section-15.5.10",
    StatusCodes.Status500InternalServerError => "https://tools.ietf.org/html/rfc9110#section-15.6.1",
    StatusCodes.Status501NotImplemented => "https://tools.ietf.org/html/rfc9110#section-15.6.2",
    StatusCodes.Status503ServiceUnavailable => "https://tools.ietf.org/html/rfc9110#section-15.6.4",
    _ => "https://tools.ietf.org/html/rfc9110#section-15.6.1"
  };
}
