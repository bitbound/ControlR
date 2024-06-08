using Microsoft.Extensions.Logging;
using ControlR.Libraries.Shared.Helpers;
using System.Runtime.CompilerServices;
using ControlR.Libraries.Shared.Primitives;

namespace ControlR.Libraries.Shared.Extensions;

public static class ILoggerExtensions
{
    public static IDisposable? BeginMemberScope<T>(this ILogger<T> logger, [CallerMemberName] string callerMemberName = "")
    {
        return logger.BeginScope(callerMemberName);
    }

    public static Result<ResultT> LogResult<T, ResultT>(
        this ILogger<T> logger,
        Result<ResultT> result,
        [CallerMemberName] string callerName = "")
    {
        using var logScope = string.IsNullOrWhiteSpace(callerName) ?
            new NoopDisposable() :
            logger.BeginScope(callerName);


        if (result.IsSuccess)
        {
            logger.LogInformation("Successful result.");
        }
        else if (result.HadException)
        {
            logger.LogError(result.Exception, "Error result.");
        }
        else
        {
            logger.LogWarning("Failed result. Reason: {reason}", result.Reason);
        }
        return result;
    }

    public static Result LogResult<T>(
      this ILogger<T> logger,
      Result result,
      [CallerMemberName] string callerName = "")
    {
        using var logScope = string.IsNullOrWhiteSpace(callerName) ?
            new NoopDisposable() :
            logger.BeginScope(callerName);


        if (result.IsSuccess)
        {
            logger.LogInformation("Successful result.");
        }
        else if (result.HadException)
        {
            logger.LogError(result.Exception, "Error result.");
        }
        else
        {
            logger.LogWarning("Failed result. Reason: {reason}", result.Reason);
        }
        return result;
    }
}
