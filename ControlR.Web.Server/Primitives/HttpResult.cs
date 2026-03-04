using System.Diagnostics.CodeAnalysis;

namespace ControlR.Web.Server.Primitives;

/// <summary>
/// Describes the success or failure of an HTTP operation with error codes.
/// This is designed to be used by services invoked by HTTP endpoints, so the result can
/// be converted to an appropriate HTTP response based on the error code and reason.
/// </summary>
public class HttpResult
{
  public HttpResult(bool isSuccess, HttpResultErrorCode errorCode = HttpResultErrorCode.None, string reason = "")
  {
    if (!isSuccess && errorCode == HttpResultErrorCode.None)
    {
      throw new ArgumentException("An error code must be supplied for an unsuccessful result.");
    }

    if (isSuccess && errorCode != HttpResultErrorCode.None)
    {
      throw new ArgumentException("Error code must be None for successful results.");
    }

    IsSuccess = isSuccess;
    ErrorCode = errorCode;
    Reason = reason;
  }

  public HttpResult(bool isSuccess, Exception? exception, HttpResultErrorCode errorCode, string reason)
  {
    if (!isSuccess && errorCode == HttpResultErrorCode.None)
    {
      throw new ArgumentException("An error code must be supplied for an unsuccessful result.");
    }

    IsSuccess = isSuccess;
    Exception = exception;
    ErrorCode = errorCode;
    Reason = reason;
  }

  private HttpResult(Exception ex, HttpResultErrorCode errorCode)
  {
    IsSuccess = false;
    Reason = ex.Message;
    Exception = ex;
    ErrorCode = errorCode;
  }

  private HttpResult(Exception ex, HttpResultErrorCode errorCode, string reason)
  {
    IsSuccess = false;
    Reason = reason;
    Exception = ex;
    ErrorCode = errorCode;
  }

  public HttpResultErrorCode ErrorCode { get; init; }

  public Exception? Exception { get; init; }

  [MemberNotNullWhen(true, nameof(Exception))]
  public bool HadException => Exception is not null;

  [MemberNotNullWhen(false, nameof(Reason))]
  public bool IsSuccess { get; init; }

  public string Reason { get; init; } = string.Empty;

  public static HttpResult Fail(HttpResultErrorCode errorCode, string reason)
  {
    return new HttpResult(false, errorCode, reason);
  }

  public static HttpResult Fail(Exception ex, HttpResultErrorCode errorCode)
  {
    return new HttpResult(ex, errorCode);
  }

  public static HttpResult Fail(Exception ex, HttpResultErrorCode errorCode, string reason)
  {
    return new HttpResult(ex, errorCode, reason);
  }

  public static HttpResult<T> Fail<T>(HttpResultErrorCode errorCode, string reason)
  {
    return new HttpResult<T>(errorCode, reason);
  }

  public static HttpResult<T> Fail<T>(Exception ex, HttpResultErrorCode errorCode)
  {
    return new HttpResult<T>(ex, errorCode);
  }

  public static HttpResult<T> Fail<T>(Exception ex, HttpResultErrorCode errorCode, string reason)
  {
    return new HttpResult<T>(ex, errorCode, reason);
  }

  public static HttpResult Ok()
  {
    return new HttpResult(true);
  }

  public static HttpResult<T> Ok<T>(T value)
  {
    return new HttpResult<T>(value);
  }
}

/// <summary>
/// Describes the success or failure of an HTTP operation with error codes and a value.
/// </summary>
public class HttpResult<T>
{
  /// <summary>
  /// Returns a successful result with the given value.
  /// </summary>
  /// <param name="value"></param>
  public HttpResult(T value)
  {
    IsSuccess = true;
    Value = value;
    ErrorCode = HttpResultErrorCode.None;
  }

  /// <summary>
  /// Returns an unsuccessful result with the given error code and exception.
  /// </summary>
  /// <param name="exception"></param>
  /// <param name="errorCode"></param>
  public HttpResult(Exception exception, HttpResultErrorCode errorCode)
  {
    IsSuccess = false;
    Exception = exception;
    ErrorCode = errorCode;
    Reason = exception.Message;
  }

  /// <summary>
  /// Returns an unsuccessful result with the given error code, exception and reason.
  /// </summary>
  /// <param name="exception"></param>
  /// <param name="errorCode"></param>
  /// <param name="reason"></param>
  public HttpResult(Exception exception, HttpResultErrorCode errorCode, string reason)
  {
    IsSuccess = false;
    Exception = exception;
    ErrorCode = errorCode;
    Reason = reason;
  }

  /// <summary>
  ///  Returns an unsuccessful result with the given error code and reason.
  /// </summary>
  /// <param name="errorCode"></param>
  /// <param name="reason"></param>
  public HttpResult(HttpResultErrorCode errorCode, string reason)
  {
    IsSuccess = false;
    ErrorCode = errorCode;
    Reason = reason;
  }

  public HttpResultErrorCode ErrorCode { get; init; }

  public Exception? Exception { get; init; }

  [MemberNotNullWhen(true, nameof(Exception))]
  public bool HadException => Exception is not null;

  [MemberNotNullWhen(true, nameof(Value))]
  [MemberNotNullWhen(false, nameof(Reason))]
  public bool IsSuccess { get; init; }

  public string Reason { get; init; } = string.Empty;

  public T? Value { get; init; }

  public HttpResult ToHttpResult()
  {
    return new HttpResult(IsSuccess, Exception, ErrorCode, Reason);
  }

  public HttpResult<TNewValue> ToHttpResult<TNewValue>(TNewValue value)
  {
    if (IsSuccess)
    {
      return HttpResult.Ok(value);
    }
    return HttpResult.Fail<TNewValue>(
      Exception ?? new InvalidOperationException(Reason),
      ErrorCode,
      Reason);
  }
}
