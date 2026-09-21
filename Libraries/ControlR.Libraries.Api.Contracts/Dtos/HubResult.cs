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
  public HubResult(
    bool isSuccess,
    string? reason = null,
    OperationFailureCode failureCode = OperationFailureCode.Unknown)
  {
    if (!isSuccess && string.IsNullOrWhiteSpace(reason))
    {
      throw new ArgumentException("A reason must be supplied for an unsuccessful result.");
    }

    IsSuccess = isSuccess;
    Reason = reason;
    FailureCode = failureCode;
  }

  /// <summary>
  /// Machine-readable reason for a failure. <see cref="OperationFailureCode.Unknown" /> for a success
  /// or for a refusal from an agent that predates this field.
  /// </summary>
  public OperationFailureCode FailureCode { get; init; }

  [MemberNotNullWhen(false, nameof(Reason))]
  public virtual bool IsSuccess { get; init; }

  public string? Reason { get; init; }

  public static HubResult Fail(string reason, OperationFailureCode failureCode = OperationFailureCode.Unknown)
  {
    return new HubResult(false, reason, failureCode);
  }

  public static HubResult<T> Fail<T>(string reason, OperationFailureCode failureCode = OperationFailureCode.Unknown)
  {
    return new HubResult<T>(value: default, isSuccess: false, reason, failureCode);
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
public class HubResult<T> : HubResult
{
  [JsonConstructor]
  [SerializationConstructor]
  public HubResult(
    T? value,
    bool isSuccess,
    string? reason = null,
    OperationFailureCode failureCode = OperationFailureCode.Unknown)
    : base(isSuccess, reason, failureCode)
  {
    if (!isSuccess && string.IsNullOrWhiteSpace(reason))
    {
      throw new ArgumentException("A reason must be supplied for an unsuccessful result.");
    }

    Value = value;
    IsSuccess = isSuccess;
    Reason = reason;
    FailureCode = failureCode;
  }


  [MemberNotNullWhen(true, nameof(Value))]
  public override bool IsSuccess { get; init; }

  public T? Value { get; init; }
}
