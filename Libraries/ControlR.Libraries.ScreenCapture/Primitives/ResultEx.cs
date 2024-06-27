using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace ControlR.Libraries.ScreenCapture.Primitives;

/// <summary>
/// Describes the success or failure of any kind of operation.
/// </summary>
[DataContract]
public class ResultEx
{
    [JsonConstructor]
    public ResultEx(bool isSuccess, string reason = "")
    {
        if (!isSuccess && string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("A reason or exception must be supplied for an unsuccessful result.");
        }

        IsSuccess = isSuccess;
        Reason = reason;
    }

    public ResultEx(bool isSuccess, Exception? exception, string reason)
    {
        IsSuccess = isSuccess;
        Exception = exception;
        Reason = reason;
    }

    private ResultEx(Exception ex)
    {
        IsSuccess = false;
        Reason = ex.Message;
        Exception = ex;
    }

    private ResultEx(Exception ex, string reason)
    {
        IsSuccess = false;
        Reason = reason;
        Exception = ex;
    }

    [IgnoreDataMember]
    public Exception? Exception { get; init; }

    [IgnoreDataMember]
    [MemberNotNullWhen(true, nameof(Exception))]
    public bool HadException => Exception is not null;

    [DataMember]
    [MemberNotNullWhen(false, nameof(Reason))]
    public bool IsSuccess { get; init; }

    [DataMember]
    public string Reason { get; init; } = string.Empty;

    public static ResultEx Fail(string reason)
    {
        return new ResultEx(false, reason);
    }

    public static ResultEx Fail(Exception ex)
    {
        return new ResultEx(ex);
    }

    public static ResultEx Fail(Exception ex, string reason)
    {
        return new ResultEx(ex, reason);
    }

    public static ResultEx<T> Fail<T>(string reason)
    {
        return new ResultEx<T>(reason);
    }

    public static ResultEx<T> Fail<T>(Exception ex)
    {
        return new ResultEx<T>(ex);
    }

    public static ResultEx<T> Fail<T>(Exception ex, string reason)
    {
        return new ResultEx<T>(ex, reason);
    }

    public static ResultEx Ok()
    {
        return new ResultEx(true);
    }

    public static ResultEx<T> Ok<T>(T value)
    {
        return new ResultEx<T>(value);
    }
}

/// <summary>
/// Describes the success or failure of any kind of operation.
/// </summary>
[DataContract]
public class ResultEx<T>
{
    [JsonConstructor]
    public ResultEx(T? value, bool isSuccess, string reason)
    {
        Value = value;
        IsSuccess = isSuccess;
        Reason = reason;
    }

    /// <summary>
    /// Returns a successful result with the given value.
    /// </summary>
    /// <param name="value"></param>
    public ResultEx(T value)
    {
        IsSuccess = true;
        Value = value;
    }

    /// <summary>
    /// Returns an unsuccessful result with the given exception.
    /// </summary>
    /// <param name="exception"></param>
    public ResultEx(Exception exception)
    {
        IsSuccess = false;
        Exception = exception;
        Reason = exception.Message;
    }

    /// <summary>
    /// Returns an unsuccessful result with the given exception and reason.
    /// </summary>
    /// <param name="exception"></param>
    public ResultEx(Exception exception, string reason)
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
    public ResultEx(string reason)
    {
        IsSuccess = false;
        Reason = reason;
    }

    [IgnoreDataMember]
    public Exception? Exception { get; init; }

    [IgnoreDataMember]
    [MemberNotNullWhen(true, nameof(Exception))]
    public bool HadException => Exception is not null;

    [MemberNotNullWhen(true, nameof(Value))]
    [MemberNotNullWhen(false, nameof(Reason))]
    [DataMember]
    public bool IsSuccess { get; init; }

    [DataMember]
    public string Reason { get; init; } = string.Empty;

    [DataMember]
    public T? Value { get; init; }


    public ResultEx ToResult()
    {
        return new ResultEx(IsSuccess, Exception, Reason);
    }
}