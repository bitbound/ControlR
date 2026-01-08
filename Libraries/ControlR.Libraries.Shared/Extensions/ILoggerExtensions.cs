using ControlR.Libraries.Shared.Helpers;
using ControlR.Libraries.Shared.Logging;
using Microsoft.Extensions.Caching.Memory;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace ControlR.Libraries.Shared.Extensions;

public static class LoggerExtensions
{
  private static readonly MemoryCache _cache = new(new MemoryCacheOptions());

  public static IDisposable? BeginMemberScope<T>(
    this ILogger<T> logger,
    [CallerMemberName] string callerMemberName = "")
  {
    return logger.BeginScope(callerMemberName);
  }

  [SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Helps make it discoverable.")]
  public static IDisposable EnterDedupeScope<T>(this ILogger<T> logger)
  {
    return LogDeduplicationContext.EnterScope();
  }

  public static void LogCriticalDeduped<T>(
    this ILogger<T> logger,
    string template,
    [CallerFilePath] string callerFilePath = "",
    [CallerLineNumber] int callerLineNumber = 0,
    [CallerMemberName] string callerMemberName = "",
    Exception? exception = null,
    TimeSpan? cacheDuration = null,
    params object?[] args)
  {
    logger.LogDeduped(
      LogLevel.Critical,
      template,
      callerFilePath,
      callerLineNumber,
      callerMemberName,
      exception,
      cacheDuration,
      args);
  }

  public static void LogDebugDeduped<T>(
    this ILogger<T> logger,
    string template,
    [CallerFilePath] string callerFilePath = "",
    [CallerLineNumber] int callerLineNumber = 0,
    [CallerMemberName] string callerMemberName = "",
    Exception? exception = null,
    TimeSpan? cacheDuration = null,
    params object?[] args)
  {
    logger.LogDeduped(
      LogLevel.Debug,
      template,
      callerFilePath,
      callerLineNumber,
      callerMemberName,
      exception,
      cacheDuration,
      args);
  }

  [SuppressMessage("Usage", "CA2254:Template should be a static expression")]
  public static void LogDeduped<T>(
    this ILogger<T> logger,
    LogLevel logLevel,
    string template,
    [CallerFilePath] string callerFilePath = "",
    [CallerLineNumber] int callerLineNumber = 0,
    [CallerMemberName] string callerMemberName = "",
    Exception? exception = null,
    TimeSpan? cacheDuration = null,
    params object?[] args)
  {
    if (LogDeduplicationContext.IsEnabled)
    {
      var argsKey = args != null && args.Length > 0
        ? string.Join("|", args.Select(a => a?.ToString() ?? "<null>"))
        : string.Empty;

      cacheDuration ??= TimeSpan.FromHours(1);
      var key = $"{callerFilePath}:{callerLineNumber}:{callerMemberName}:{template}:{argsKey}";

      if (_cache.TryGetValue(key, out LogRecord? cachedValue) &&
         cachedValue is not null)
      {
        cachedValue.TimesLogged++;
        return;
      }

      var logEntry = new LogRecord
      {
        Args = args ?? [],
        LogLevel = logLevel,
        Template = template,
        Exception = exception
      };

      var entryOptions = GetEntryOptions(cacheDuration.Value, logger);
      _cache.Set(key, logEntry, entryOptions);
    }

    logger.Log(logLevel, exception, template, args ?? []);
  }

  public static void LogErrorDeduped<T>(
    this ILogger<T> logger,
    string template,
    [CallerFilePath] string callerFilePath = "",
    [CallerLineNumber] int callerLineNumber = 0,
    [CallerMemberName] string callerMemberName = "",
    Exception? exception = null,
    TimeSpan? cacheDuration = null,
    params object?[] args)
  {
    logger.LogDeduped(
      LogLevel.Error,
      template,
      callerFilePath,
      callerLineNumber,
      callerMemberName,
      exception,
      cacheDuration,
      args);
  }

  public static void LogInformationDeduped<T>(
    this ILogger<T> logger,
    string template,
    [CallerFilePath] string callerFilePath = "",
    [CallerLineNumber] int callerLineNumber = 0,
    [CallerMemberName] string callerMemberName = "",
    Exception? exception = null,
    TimeSpan? cacheDuration = null,
    params object?[] args)
  {
    logger.LogDeduped(
      LogLevel.Information,
      template,
      callerFilePath,
      callerLineNumber,
      callerMemberName,
      exception,
      cacheDuration,
      args);
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

  public static void LogWarningDeduped<T>(
    this ILogger<T> logger,
    string template,
    [CallerFilePath] string callerFilePath = "",
    [CallerLineNumber] int callerLineNumber = 0,
    [CallerMemberName] string callerMemberName = "",
    Exception? exception = null,
    TimeSpan? cacheDuration = null,
    params object?[] args)
  {
    logger.LogDeduped(
      LogLevel.Warning,
      template,
      callerFilePath,
      callerLineNumber,
      callerMemberName,
      exception,
      cacheDuration,
      args);
  }

  [SuppressMessage("Usage", "CA2254:Template should be a static expression")]
  private static MemoryCacheEntryOptions GetEntryOptions<T>(TimeSpan cacheDuration, ILogger<T> logger)
  {
    var entryOptions = new MemoryCacheEntryOptions
    {
      AbsoluteExpirationRelativeToNow = cacheDuration,
    };
    entryOptions.PostEvictionCallbacks.Add(new()
    {
      EvictionCallback = (k, v, reason, state) =>
      {
        if (v is LogRecord lr && lr.TimesLogged > 0)
        {
          var template = $"{lr.Template} (Repeated {lr.TimesLogged} time(s))";
          logger.Log(lr.LogLevel, lr.Exception, template, lr.Args);
        }
      }
    });
    return entryOptions;
  }

  private class LogRecord
  {
    public object?[] Args { get; init; } = [];
    public Exception? Exception { get; init; }
    public LogLevel LogLevel { get; init; }
    public string Template { get; init; } = string.Empty;
    public int TimesLogged { get; set; }
  }
}