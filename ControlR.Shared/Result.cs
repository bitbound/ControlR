using ControlR.Shared.Serialization;
using MessagePack;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;


namespace ControlR.Shared;

/// <summary>
/// Describes the success or failure of any kind of operation.
/// </summary>
[MessagePackObject]
public class Result
{
    [JsonConstructor]
    private Result(bool isSuccess, string reason = "", Exception? exception = null)
    {
        if (!isSuccess && exception is null && string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("A reason or exception must be supplied for an unsuccessful result.");
        }

        IsSuccess = isSuccess;
        Exception = exception;
        Reason = string.IsNullOrWhiteSpace(reason) ?
            exception?.Message ?? string.Empty :
            reason;
    }

    [MsgPackKey]
    public Exception? Exception { get; init; }

    [IgnoreDataMember]
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
        return new Result(false, string.Empty, ex);
    }

    public static Result<T> Fail<T>(string reason)
    {
        return new Result<T>(reason);
    }

    public static Result<T> Fail<T>(Exception ex)
    {
        return new Result<T>(ex);
    }

    public static Result Ok()
    {
        return new Result(true);
    }

    public static Result<T> Ok<T>(T value)
    {
        return new Result<T>(value);
    }
}


/// <summary>
/// Describes the success or failure of any kind of operation.
/// </summary>
[MessagePackObject]
public class Result<T>
{
    [JsonConstructor]
    public Result(T? value, bool isSuccess, Exception? exception, string reason)
    {
        Value = value;
        IsSuccess = isSuccess;
        Exception = exception;
        Value = value;
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
    /// Returns an unsuccessful result with the given reason.
    /// </summary>
    /// <param name="errorMessage"></param>
    /// <exception cref="ArgumentException"></exception>
    public Result(string reason)
    {
        IsSuccess = false;
        Reason = reason;
    }

    [MsgPackKey]
    public Exception? Exception { get; init; }

    [IgnoreDataMember]
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
}

