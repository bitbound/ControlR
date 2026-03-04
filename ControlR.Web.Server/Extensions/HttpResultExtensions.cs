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

    return result.ErrorCode switch
    {
      HttpResultErrorCode.NotFound => new NotFoundObjectResult(result.Reason),
      HttpResultErrorCode.Conflict => new ConflictObjectResult(result.Reason),
      HttpResultErrorCode.BadRequest => new BadRequestObjectResult(result.Reason),
      HttpResultErrorCode.Unauthorized => new UnauthorizedObjectResult(result.Reason),
      HttpResultErrorCode.Forbidden => new ObjectResult(result.Reason)
      {
        StatusCode = StatusCodes.Status403Forbidden
      },
      HttpResultErrorCode.ValidationFailed => new BadRequestObjectResult(result.Reason),
      HttpResultErrorCode.ServiceUnavailable => new ObjectResult(result.Reason)
      {
        StatusCode = StatusCodes.Status503ServiceUnavailable
      },
      HttpResultErrorCode.NotImplemented => new ObjectResult(result.Reason)
      {
        StatusCode = StatusCodes.Status501NotImplemented
      },
      _ => new ObjectResult(result.Reason)
      {
        StatusCode = StatusCodes.Status500InternalServerError
      }
    };
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

    return result.ErrorCode switch
    {
      HttpResultErrorCode.NotFound => new NotFoundObjectResult(result.Reason),
      HttpResultErrorCode.Conflict => new ConflictObjectResult(result.Reason),
      HttpResultErrorCode.BadRequest => new BadRequestObjectResult(result.Reason),
      HttpResultErrorCode.Unauthorized => new UnauthorizedObjectResult(result.Reason),
      HttpResultErrorCode.Forbidden => new ObjectResult(result.Reason)
      {
        StatusCode = StatusCodes.Status403Forbidden
      },
      HttpResultErrorCode.ValidationFailed => new BadRequestObjectResult(result.Reason),
      HttpResultErrorCode.ServiceUnavailable => new ObjectResult(result.Reason)
      {
        StatusCode = StatusCodes.Status503ServiceUnavailable
      },
      HttpResultErrorCode.NotImplemented => new ObjectResult(result.Reason)
      {
        StatusCode = StatusCodes.Status501NotImplemented
      },
      _ => new ObjectResult(result.Reason)
      {
        StatusCode = StatusCodes.Status500InternalServerError
      }
    };
  }
}
