using ControlR.Libraries.Shared.Helpers;
using ControlR.Libraries.Shared.Logging;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace ControlR.Libraries.Shared.Extensions;

public static class LoggerExtensions
{
  public static IDisposable? BeginMemberScope<T>(
    this ILogger<T> logger,
    [CallerMemberName] string callerMemberName = "")
  {
    return logger.BeginScope(callerMemberName);
  }

  [SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Helps make it discoverable.")]
  public static LogDeduplicationContext<T> EnterDedupeScope<T>(
    this ILogger<T> logger,
    TimeSpan? cacheDuration = null)
  {
    return new LogDeduplicationContext<T>(logger, cacheDuration);
  }

  public static Result<TResultT> LogResult<T, TResultT>(
    this ILogger<T> logger,
    Result<TResultT> result,
    [CallerMemberName] string callerName = "")
  {
    using var logScope = string.IsNullOrWhiteSpace(callerName) ? new NoopDisposable() : logger.BeginScope(callerName);

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

  public static HubResult<TValue> LogResult<TLogger, TValue>(
    this ILogger<TLogger> logger,
    HubResult<TValue> result,
    [CallerMemberName] string callerName = "")
  {
    using var logScope = string.IsNullOrWhiteSpace(callerName)
      ? new NoopDisposable()
      : logger.BeginScope(callerName);

    if (result.IsSuccess)
    {
      logger.LogInformation("Successful result.");
    }
    else
    {
      logger.LogWarning("Failed result. Reason: {reason}", result.Reason);
    }

    return result;
  }

  public static HubResult LogResult<TLogger>(
    this ILogger<TLogger> logger,
    HubResult result,
    [CallerMemberName] string callerName = "")
  {
    using var logScope = string.IsNullOrWhiteSpace(callerName)
      ? new NoopDisposable()
      : logger.BeginScope(callerName);

    if (result.IsSuccess)
    {
      logger.LogInformation("Successful result.");
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
    using var logScope = string.IsNullOrWhiteSpace(callerName)
      ? new NoopDisposable()
      : logger.BeginScope(callerName);

    if (result.IsSuccess)
    {
      logger.LogInformation("Successful result.");
    }
    else if (result.HadException)
    {
      logger.LogError(result.Exception, "Error result. Reason: {Reason}", result.Reason);
    }
    else
    {
      logger.LogWarning("Failed result. Reason: {reason}", result.Reason);
    }

    return result;
  }

}