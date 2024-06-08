using ControlR.Libraries.Shared.Extensions;
using ControlR.Libraries.Shared.Serialization;
using MessagePack;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace ControlR.Libraries.Shared.Primitives;

/// <summary>
/// Describes the success or failure of any kind of operation.
/// </summary>
[MessagePackObject(true)]
public class Result
{
    [JsonConstructor]
    [SerializationConstructor]
    public Result(bool isSuccess, string reason = "")
    {
        if (!isSuccess && string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("A reason or exception must be supplied for an unsuccessful result.");
        }

        IsSuccess = isSuccess;
        Reason = reason;
    }

    public Result(bool isSuccess, Exception? exception, string reason)
    {
        IsSuccess = isSuccess;
        Exception = exception;
        Reason = reason;
    }

    private Result(Exception ex)
    {
        IsSuccess = false;
        Reason = ex.Message;
        Exception = ex;
    }

    private Result(Exception ex, string reason)
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

    [MsgPackKey]
    [MemberNotNullWhen(false, nameof(Reason))]
    public bool IsSuccess { get; init; }

    [MsgPackKey]
    public string Reason { get; init; } = string.Empty;

    public static Result Fail(string reason)
    {
        return new Result(false, reason);
    }

    public static Result Fail(Exception ex)
    {
        return new Result(ex);
    }

    public static Result Fail(Exception ex, string reason)
    {
        return new Result(ex, reason);
    }

    public static Result<T> Fail<T>(string reason)
    {
        return new Result<T>(reason);
    }

    public static Result<T> Fail<T>(Exception ex)
    {
        return new Result<T>(ex);
    }

    public static Result<T> Fail<T>(Exception ex, string reason)
    {
        return new Result<T>(ex, reason);
    }

    public static Result Ok()
    {
        return new Result(true);
    }

    public static Result<T> Ok<T>(T value)
    {
        return new Result<T>(value);
    }

    public Result Log<TLogger>(ILogger<TLogger> logger)
    {
        logger.LogResult(this);
        return this;
    }
}

/// <summary>
/// Describes the success or failure of any kind of operation.
/// </summary>
[MessagePackObject(true)]
public class Result<T>
{
    [JsonConstructor]
    [SerializationConstructor]
    public Result(T? value, bool isSuccess, string reason)
    {
        Value = value;
        IsSuccess = isSuccess;
        Reason = reason;
    }

    /// <summary>
    /// Returns a successful result with the given value.
    /// </summary>
    /// <param name="value"></param>
    public Result(T value)
    {
        IsSuccess = true;
        Value = value;
    }

    /// <summary>
    /// Returns an unsuccessful result with the given exception.
    /// </summary>
    /// <param name="exception"></param>
    public Result(Exception exception)
    {
        IsSuccess = false;
        Exception = exception;
        Reason = exception.Message;
    }

    /// <summary>
    /// Returns an unsuccessful result with the given exception and reason.
    /// </summary>
    /// <param name="exception"></param>
    public Result(Exception exception, string reason)
    {
        IsSuccess = false;
        Exception = exception;
        Reason = reason;
    }

    /// <summary>
    /// Returns an unsuccessful result with the given reason.
    /// </summary>
    /// <param name="errorMessage"></param>
    /// <exception cref="ArgumentException"></exception>
    public Result(string reason)
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

    [MsgPackKey]
    [MemberNotNullWhen(true, nameof(Value))]
    [MemberNotNullWhen(false, nameof(Reason))]
    public bool IsSuccess { get; init; }

    [MsgPackKey]
    public string Reason { get; init; } = string.Empty;

    [MsgPackKey]
    public T? Value { get; init; }

    public Result<T> Log<TLogger>(ILogger<TLogger> logger)
    {
        logger.LogResult(this);
        return this;
    }

    public Result ToResult()
    {
        return new Result(IsSuccess, Exception, Reason);
    }

    public Result<TNewValue> ToResult<TNewValue>(TNewValue value)
    {
        if (IsSuccess)
        {
            return Result.Ok(value);
        }
        return Result.Fail<TNewValue>(Exception ?? new Exception(Reason), Reason);
    }
}