using System.Runtime.CompilerServices;
using ControlR.Libraries.Shared.Helpers;
using Microsoft.Extensions.Caching.Memory;

namespace ControlR.Libraries.Shared.Extensions;

public static class LoggerExtensions
{
  private static readonly MemoryCache _cache = new(new MemoryCacheOptions());

  public static IDisposable? BeginMemberScope<T>(this ILogger<T> logger,
    [CallerMemberName] string callerMemberName = "")
  {
    return logger.BeginScope(callerMemberName);
  }

  public static void LogIfChanged<T>(
    this ILogger<T> logger,
    LogLevel logLevel,
    string template,
    [CallerFilePath] string callerFilePath = "",
    [CallerLineNumber] int callerLineNumber = 0,
    [CallerMemberName] string callerMemberName = "",
    Exception? exception = null,
    params object[] args)
  {
    var argsKey = args != null && args.Length > 0
      ? string.Join("|", args.Select(a => a?.ToString() ?? "<null>")) 
      : string.Empty;

    var cacheValue = $"{template}|{argsKey}";
    var key = $"{callerFilePath}:{callerLineNumber}:{callerMemberName}:{template}";
    if (_cache.TryGetValue(key, out string? cachedValue) && cachedValue == cacheValue)
    {
      return;
    }

    _cache.Set(key, cacheValue, TimeSpan.FromHours(1));
#pragma warning disable CA2254 // Template should be a static expression
    logger.Log(logLevel, exception, template, args ?? []);
#pragma warning restore CA2254 // Template should be a static expression
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