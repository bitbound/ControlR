using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json.Serialization;

namespace ControlR.Libraries.Api.Contracts.Dtos;


/// <summary>
/// Describes the success or failure of any kind of operation.
/// </summary>
[MessagePackObject(keyAsPropertyName: true)]
public class ApiResult
{
  [JsonConstructor]
  [SerializationConstructor]
  public ApiResult(
    bool isSuccess,
    HttpStatusCode? statusCode,
    string? reason = null,
    HttpRequestError? httpRequestError = null)
  {
    if (!isSuccess && string.IsNullOrWhiteSpace(reason))
    {
      throw new ArgumentException("A reason must be supplied for an unsuccessful result.");
    }

    IsSuccess = isSuccess;
    Reason = reason;
    StatusCode = statusCode;
    HttpRequestError = httpRequestError;
  }

  public HttpRequestError? HttpRequestError { get; }
  [MemberNotNullWhen(false, nameof(Reason))]
  public bool IsSuccess { get; init; }
  public string? Reason { get; init; }
  public HttpStatusCode? StatusCode { get; }

  public static ApiResult Fail(string reason, HttpStatusCode? statusCode = null, HttpRequestError? httpRequestError = null)
  {
    return new ApiResult(false, statusCode, reason, httpRequestError);
  }

  public static ApiResult<T> Fail<T>(string reason, HttpStatusCode? statusCode = null, HttpRequestError? httpRequestError = null)
  {
    return new ApiResult<T>(value: default, false, statusCode, reason, httpRequestError);
  }

  public static ApiResult Ok()
  {
    return new ApiResult(true, HttpStatusCode.OK);
  }

  public static ApiResult<T> Ok<T>(T value)
  {
    return new ApiResult<T>(value, isSuccess: true, statusCode: HttpStatusCode.OK);
  }

  public override string ToString()
  {
    var builder = new StringBuilder();
    builder.Append($"IsSuccess: {IsSuccess}");

    if (StatusCode is not null)
    {
      builder.Append($", StatusCode: {(int)StatusCode.Value} ({StatusCode.Value})");
    }

    if (HttpRequestError is not null)
    {
      builder.Append($", HttpRequestError: {HttpRequestError}");
    }

    if (!string.IsNullOrWhiteSpace(Reason))
    {
      builder.Append($", Reason: {Reason}");
    }

    return builder.ToString();
  }
}

/// <summary>
/// Describes the success or failure of any kind of operation.
/// </summary>
[MessagePackObject(keyAsPropertyName: true)]
public class ApiResult<T>
{
  [JsonConstructor]
  [SerializationConstructor]
  public ApiResult(
    T? value,
    bool isSuccess,
    HttpStatusCode? statusCode,
    string? reason = null,
    HttpRequestError? httpRequestError = null)
  {
    if (!isSuccess && string.IsNullOrWhiteSpace(reason))
    {
      throw new ArgumentException("A reason must be supplied for an unsuccessful result.");
    }

    Value = value;
    IsSuccess = isSuccess;
    Reason = reason;
    StatusCode = statusCode;
    HttpRequestError = httpRequestError;
  }

  public HttpRequestError? HttpRequestError { get; }
  [MemberNotNullWhen(true, nameof(Value))]
  [MemberNotNullWhen(false, nameof(Reason))]
  public bool IsSuccess { get; init; }
  public string? Reason { get; init; }
  public HttpStatusCode? StatusCode { get; }
  public T? Value { get; init; }

  public override string ToString()
  {
    var builder = new StringBuilder();
    builder.Append($"IsSuccess: {IsSuccess}");

    if (StatusCode is not null)
    {
      builder.Append($", StatusCode: {(int)StatusCode.Value} ({StatusCode.Value})");
    }

    if (HttpRequestError is not null)
    {
      builder.Append($", HttpRequestError: {HttpRequestError}");
    }

    if (!string.IsNullOrWhiteSpace(Reason))
    {
      builder.Append($", Reason: {Reason}");
    }

    return builder.ToString();
  }
}