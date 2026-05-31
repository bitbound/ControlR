using ControlR.Libraries.Shared.Services;
using Microsoft.Extensions.Caching.Memory;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace ControlR.Libraries.Shared.Logging;

/// <summary>
/// Static log deduplication backed by a shared <see cref="MemoryCache"/>.
/// </summary>
/// <remarks>
/// Cache entries use <see cref="MemoryCacheEntryOptions.AbsoluteExpirationRelativeToNow"/>
/// to auto-expire, and <see cref="MemoryCacheEntryOptions.PostEvictionCallbacks"/> to flush
/// accumulated <see cref="LogRecord.TimesLogged"/> counts when entries evict.
/// </remarks>
public static class ILoggerDedupingExtensions
{
  private static readonly MemoryCache _cache = new(new MemoryCacheOptions());
  private static readonly object _syncLock = new();

  public static void LogCriticalDeduped<T>(
      this ILogger<T> logger,
      string template,
      Exception? exception = null,
      TimeSpan? cacheDuration = null,
      [CallerFilePath] string callerFilePath = "",
      [CallerLineNumber] int callerLineNumber = 0,
      [CallerMemberName] string callerMemberName = "",
      params object?[] args)
  {
    LogDedupedInternal(logger, LogLevel.Critical, template, exception, cacheDuration, callerFilePath, callerLineNumber, callerMemberName, args);
  }

  public static void LogDebugDeduped<T>(
    this ILogger<T> logger,
    string template,
    Exception? exception = null,
    TimeSpan? cacheDuration = null,
    [CallerFilePath] string callerFilePath = "",
    [CallerLineNumber] int callerLineNumber = 0,
    [CallerMemberName] string callerMemberName = "",
    params object?[] args)
  {
    LogDedupedInternal(logger, LogLevel.Debug, template, exception, cacheDuration, callerFilePath, callerLineNumber, callerMemberName, args);
  }

  public static void LogDeduped<T>(
    this ILogger<T> logger,
    LogLevel logLevel,
    string template,
    Exception? exception = null,
    TimeSpan? cacheDuration = null,
    [CallerFilePath] string callerFilePath = "",
    [CallerLineNumber] int callerLineNumber = 0,
    [CallerMemberName] string callerMemberName = "",
    params object?[] args)
  {
    LogDedupedInternal(
      logger,
      logLevel,
      template,
      exception,
      cacheDuration,
      callerFilePath,
      callerLineNumber,
      callerMemberName,
      args);
  }

  public static void LogErrorDeduped<T>(
    this ILogger<T> logger,
    string template,
    Exception? exception = null,
    TimeSpan? cacheDuration = null,
    [CallerFilePath] string callerFilePath = "",
    [CallerLineNumber] int callerLineNumber = 0,
    [CallerMemberName] string callerMemberName = "",
    params object?[] args)
  {
    LogDedupedInternal(logger, LogLevel.Error, template, exception, cacheDuration, callerFilePath, callerLineNumber, callerMemberName, args);
  }

  public static void LogInformationDeduped<T>(
    this ILogger<T> logger,
    string template,
    Exception? exception = null,
    TimeSpan? cacheDuration = null,
    [CallerFilePath] string callerFilePath = "",
    [CallerLineNumber] int callerLineNumber = 0,
    [CallerMemberName] string callerMemberName = "",
    params object?[] args)
  {
    LogDedupedInternal(logger, LogLevel.Information, template, exception, cacheDuration, callerFilePath, callerLineNumber, callerMemberName, args);
  }

  public static void LogWarningDeduped<T>(
    this ILogger<T> logger,
    string template,
    Exception? exception = null,
    TimeSpan? cacheDuration = null,
    [CallerFilePath] string callerFilePath = "",
    [CallerLineNumber] int callerLineNumber = 0,
    [CallerMemberName] string callerMemberName = "",
    params object?[] args)
  {
    LogDedupedInternal(logger, LogLevel.Warning, template, exception, cacheDuration, callerFilePath, callerLineNumber, callerMemberName, args);
  }

  private static TimeSpan DefaultDuration()
  {
    // Mirrors the per-instance behavior from the old LogDeduplicationContext.
    return SystemEnvironment.Instance.IsDebug
      ? TimeSpan.FromSeconds(10)
      : TimeSpan.FromHours(1);
  }

  [SuppressMessage("Usage", "CA2254:Template should be a static expression")]
  private static void FlushLogRecord<T>(ILogger<T> logger, LogRecord logRecord)
  {
    if (logRecord.TimesLogged <= 0)
    {
      return;
    }

    var template = $"(DEDUPED: {logRecord.TimesLogged} time(s)) {logRecord.Template}";
    logger.Log(logRecord.LogLevel, logRecord.Exception, template, logRecord.Args);
  }

  [SuppressMessage("Usage", "CA2254:Template should be a static expression")]
  private static void LogDedupedInternal<T>(
      ILogger<T> logger,
      LogLevel logLevel,
      string template,
      Exception? exception,
      TimeSpan? cacheDuration,
      [CallerFilePath] string callerFilePath = "",
      [CallerLineNumber] int callerLineNumber = 0,
      [CallerMemberName] string callerMemberName = "",
      params object?[] args)
  {
    args ??= [];

    var argsKey = args.Length > 0
      ? string.Join("|", args.Select(a => a?.ToString() ?? "<null>"))
      : string.Empty;

    // Caller info is auto-filled by the compiler at the call site, giving each
    // call-site a unique key, exactly like the old per-instance context.
    var key = $"{callerFilePath}:{callerLineNumber}:{callerMemberName}:{template}:{argsKey}";

    var entryOptions = new MemoryCacheEntryOptions
    {
      AbsoluteExpirationRelativeToNow = cacheDuration ?? DefaultDuration(),
    };

    entryOptions.PostEvictionCallbacks.Add(new()
    {
      EvictionCallback = (_, value, _, _) =>
      {
        if (value is LogRecord logRecord && logRecord.TimesLogged > 0)
        {
          FlushLogRecord(logger, logRecord);
        }
      }
    });

    lock (_syncLock)
    {
      if (_cache.TryGetValue(key, out LogRecord? cachedValue) && cachedValue is not null)
      {
        cachedValue.TimesLogged++;
        return;
      }

      var logEntry = new LogRecord
      {
        Args = args,
        LogLevel = logLevel,
        Template = template,
        Exception = exception,
      };

      _cache.Set(key, logEntry, entryOptions);
    }

    // First occurrence. Log immediately.
    logger.Log(logLevel, exception, template, args);
  }

  private sealed class LogRecord
  {
    public object?[] Args { get; init; } = [];
    public Exception? Exception { get; init; }
    public LogLevel LogLevel { get; init; }
    public string Template { get; init; } = string.Empty;
    public int TimesLogged { get; set; }
  }
}
