using MessagePack;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace ControlR.Libraries.Ipc;

/// <summary>
/// Describes the success or failure of an IPC call.
/// </summary>
[MessagePackObject(keyAsPropertyName: true)]
public class IpcResult
{
  [JsonConstructor]
  [SerializationConstructor]
  public IpcResult(bool isSuccess, string reason = "")
  {
    if (!isSuccess && string.IsNullOrWhiteSpace(reason))
    {
      throw new ArgumentException("A reason or exception must be supplied for an unsuccessful result.");
    }

    IsSuccess = isSuccess;
    Reason = reason;
  }

  public IpcResult(bool isSuccess, Exception? exception, string reason)
  {
    IsSuccess = isSuccess;
    Exception = exception;
    Reason = reason;
  }

  private IpcResult(Exception ex)
  {
    IsSuccess = false;
    Reason = ex.Message;
    Exception = ex;
  }

  private IpcResult(Exception ex, string reason)
  {
    IsSuccess = false;
    Reason = reason;
    Exception = ex;
  }

  [IgnoreDataMember]
  [IgnoreMember]
  public Exception? Exception { get; init; }

  [IgnoreDataMember]
  [IgnoreMember]
  [MemberNotNullWhen(true, nameof(Exception))]
  public bool HadException => Exception is not null;

  [Key(nameof(IsSuccess))]
  [MemberNotNullWhen(false, nameof(Reason))]
  public bool IsSuccess { get; init; }

  [Key(nameof(Reason))]
  public string Reason { get; init; } = string.Empty;

  public static IpcResult Fail(string reason)
  {
    return new IpcResult(false, reason);
  }

  public static IpcResult Fail(Exception ex)
  {
    return new IpcResult(ex);
  }

  public static IpcResult Fail(Exception ex, string reason)
  {
    return new IpcResult(ex, reason);
  }

  public static IpcResult<T> Fail<T>(string reason)
  {
    return new IpcResult<T>(reason);
  }

  public static IpcResult<T> Fail<T>(Exception ex)
  {
    return new IpcResult<T>(ex);
  }

  public static IpcResult<T> Fail<T>(Exception ex, string reason)
  {
    return new IpcResult<T>(ex, reason);
  }

  public static IpcResult Ok()
  {
    return new IpcResult(true);
  }

  public static IpcResult<T> Ok<T>(T value)
  {
    return new IpcResult<T>(value);
  }
}

/// <summary>
/// Describes the success or failure of any kind of operation.
/// </summary>
[MessagePackObject(keyAsPropertyName: true)]
public class IpcResult<T>
{
  [JsonConstructor]
  [SerializationConstructor]
  public IpcResult(T? value, bool isSuccess, string reason)
  {
    Value = value;
    IsSuccess = isSuccess;
    Reason = reason;
  }

  /// <summary>
  /// Returns a successful result with the given value.
  /// </summary>
  /// <param name="value"></param>
  public IpcResult(T value)
  {
    IsSuccess = true;
    Value = value;
  }

  /// <summary>
  /// Returns an unsuccessful result with the given exception.
  /// </summary>
  /// <param name="exception"></param>
  public IpcResult(Exception exception)
  {
    IsSuccess = false;
    Exception = exception;
    Reason = exception.Message;
  }

  /// <summary>
  /// Returns an unsuccessful result with the given exception and reason.
  /// </summary>
  /// <param name="exception"></param>
  /// <param name="reason"></param>
  public IpcResult(Exception exception, string reason)
  {
    IsSuccess = false;
    Exception = exception;
    Reason = reason;
  }

  /// <summary>
  ///  Returns an unsuccessful result with the given reason.
  /// </summary>
  /// <param name="reason"></param>
  public IpcResult(string reason)
  {
    IsSuccess = false;
    Reason = reason;
  }

  [IgnoreDataMember]
  [IgnoreMember]
  public Exception? Exception { get; init; }

  [IgnoreDataMember]
  [IgnoreMember]
  [MemberNotNullWhen(true, nameof(Exception))]
  public bool HadException => Exception is not null;

  [Key(nameof(IsSuccess))]
  [MemberNotNullWhen(true, nameof(Value))]
  [MemberNotNullWhen(false, nameof(Reason))]
  public bool IsSuccess { get; init; }

  [Key(nameof(Reason))]
  public string Reason { get; init; } = string.Empty;

  [Key(nameof(Value))]
  public T? Value { get; init; }
  public IpcResult ToResult()
  {
    return new IpcResult(IsSuccess, Exception, Reason);
  }

  public IpcResult<TNewValue> ToResult<TNewValue>(TNewValue value)
  {
    if (IsSuccess)
    {
      return IpcResult.Ok(value);
    }
    return IpcResult.Fail<TNewValue>(Exception ?? new InvalidOperationException(Reason), Reason);
  }
}