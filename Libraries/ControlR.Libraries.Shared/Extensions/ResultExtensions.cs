using System.Net;
using ControlR.Libraries.Api.Contracts.Dtos;

namespace ControlR.Libraries.Shared.Extensions;

public static class ResultExtensions
{
  public static ApiResult ToApiResult(this Result result, HttpStatusCode failureStatusCode = HttpStatusCode.BadRequest)
  {
    if (result.IsSuccess)
    {
      return ApiResult.Ok();
    }

    return ApiResult.Fail(result.Reason, failureStatusCode);
  }

  public static ApiResult<T> ToApiResult<T>(this Result<T> result, HttpStatusCode failureStatusCode = HttpStatusCode.BadRequest)
  {
    if (result.IsSuccess)
    {
      return ApiResult.Ok(result.Value);
    }

    return ApiResult.Fail<T>(result.Reason, failureStatusCode);
  }

  public static HubResult ToHubResult(this Result result, Guid? errorCode = null)
  {
    if (result.IsSuccess)
    {
      return HubResult.Ok();
    }

    return HubResult.Fail(result.Reason, errorCode);
  }

  public static HubResult<T> ToHubResult<T>(this Result<T> result, Guid? errorCode = null)
  {
    if (result.IsSuccess)
    {
      return HubResult.Ok(result.Value);
    }

    if (result.Exception is not null)
    {
      return HubResult.Fail<T>(result.Reason, errorCode);
    }

    return HubResult.Fail<T>(result.Reason, errorCode);
  }

  public static Result ToResult(this ApiResult apiResult)
  {
    if (apiResult.IsSuccess)
    {
      return Result.Ok();
    }

    return Result.Fail($"Operation failed with status code {apiResult.StatusCode}. Reason: {apiResult.Reason}");
  }

  public static Result<T> ToResult<T>(this ApiResult<T> apiResult)
  {
    if (apiResult.IsSuccess)
    {
      return Result.Ok(apiResult.Value ?? Activator.CreateInstance<T>());
    }

    return Result.Fail<T>($"Operation failed with status code {apiResult.StatusCode}. Reason: {apiResult.Reason}");
  }
}