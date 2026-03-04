using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace ControlR.Libraries.Api.Contracts.Dtos;


/// <summary>
/// Describes the success or failure of any kind of operation.
/// </summary>
[MessagePackObject(keyAsPropertyName: true)]
public class HubResult
{
  [JsonConstructor]
  [SerializationConstructor]
  public HubResult(bool isSuccess, string? reason = null, Guid? errorCode = null)
  {
    if (!isSuccess && string.IsNullOrWhiteSpace(reason))
    {
      throw new ArgumentException("A reason must be supplied for an unsuccessful result.");
    }

    IsSuccess = isSuccess;
    Reason = reason;
    ErrorCode = errorCode;
  }

  public Guid? ErrorCode { get; }
  
  [MemberNotNullWhen(false, nameof(Reason))]
  public bool IsSuccess { get; init; }
  
  public string? Reason { get; init; }

  public static HubResult Fail(string reason, Guid? errorCode = null)
  {
    return new HubResult(false, reason, errorCode);
  }

  public static HubResult<T> Fail<T>(string reason, Guid? errorCode = null)
  {
    return new HubResult<T>(value: default, isSuccess: false, reason, errorCode);
  }

  public static HubResult Ok()
  {
    return new HubResult(true);
  }

  public static HubResult<T> Ok<T>(T value)
  {
    return new HubResult<T>(value, isSuccess: true);
  }

}

/// <summary>
/// Describes the success or failure of any kind of operation.
/// </summary>
[MessagePackObject(keyAsPropertyName: true)]
public class HubResult<T>
{
  [JsonConstructor]
  [SerializationConstructor]
  public HubResult(T? value, bool isSuccess, string? reason = null, Guid? errorCode = null)
  {
    if (!isSuccess && string.IsNullOrWhiteSpace(reason))
    {
      throw new ArgumentException("A reason must be supplied for an unsuccessful result.");
    }

    Value = value;
    IsSuccess = isSuccess;
    Reason = reason;
    ErrorCode = errorCode;
  }

  public Guid? ErrorCode { get; }
  [MemberNotNullWhen(true, nameof(Value))]
  [MemberNotNullWhen(false, nameof(Reason))]
  public bool IsSuccess { get; init; }

  public string? Reason { get; init; }

  public T? Value { get; init; }
}