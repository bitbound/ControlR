using ControlR.Libraries.Shared.Services;
using Microsoft.Extensions.Caching.Memory;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace ControlR.Libraries.Shared.Logging;

public sealed class LogDeduplicationContext<T>(ILogger<T> logger, TimeSpan? cacheDuration = null) : IDisposable
{
  private readonly MemoryCache _cache = new(new MemoryCacheOptions());
  private readonly TimeSpan _cacheDuration = cacheDuration ??
    (SystemEnvironment.Instance.IsDebug
      ? TimeSpan.FromSeconds(10)
      : TimeSpan.FromHours(1));

  private readonly ILogger<T> _logger = logger;
  private readonly Lock _syncLock = new();

  private bool _disposed;
  private bool _isDisposing;

  public void Dispose()
  {
    List<LogRecord> recordsToFlush = [];

    lock (_syncLock)
    {
      if (_disposed)
      {
        return;
      }

      _disposed = true;
      _isDisposing = true;
      var keys = _cache.Keys.ToArray();
      foreach (var key in keys)
      {
        if (_cache.TryGetValue(key, out LogRecord? logRecord) &&
            logRecord is not null)
        {
          recordsToFlush.Add(logRecord);
        }
      }
    }

    foreach (var logRecord in recordsToFlush)
    {
      FlushLogRecord(logRecord);
    }

    _cache.Dispose();
  }

  public void LogCriticalDeduped(
    string template,
    [CallerFilePath] string callerFilePath = "",
    [CallerLineNumber] int callerLineNumber = 0,
    [CallerMemberName] string callerMemberName = "",
    Exception? exception = null,
    params object?[] args)
  {
    LogDeduped(
      LogLevel.Critical,
      template,
      callerFilePath,
      callerLineNumber,
      callerMemberName,
      exception,
      args);
  }

  public void LogDebugDeduped(
    string template,
    [CallerFilePath] string callerFilePath = "",
    [CallerLineNumber] int callerLineNumber = 0,
    [CallerMemberName] string callerMemberName = "",
    Exception? exception = null,
    params object?[] args)
  {
    LogDeduped(
      LogLevel.Debug,
      template,
      callerFilePath,
      callerLineNumber,
      callerMemberName,
      exception,
      args);
  }

  [SuppressMessage("Usage", "CA2254:Template should be a static expression")]
  public void LogDeduped(
    LogLevel logLevel,
    string template,
    [CallerFilePath] string callerFilePath = "",
    [CallerLineNumber] int callerLineNumber = 0,
    [CallerMemberName] string callerMemberName = "",
    Exception? exception = null,
    params object?[] args)
  {
    ObjectDisposedException.ThrowIf(_disposed, this);

    var logArgs = args ?? [];
    var argsKey = logArgs.Length > 0
      ? string.Join("|", logArgs.Select(a => a?.ToString() ?? "<null>"))
      : string.Empty;

    var key = $"{callerFilePath}:{callerLineNumber}:{callerMemberName}:{template}:{argsKey}";

    lock (_syncLock)
    {
      if (_cache.TryGetValue(key, out LogRecord? cachedValue) &&
         cachedValue is not null)
      {
        cachedValue.TimesLogged++;
        return;
      }

      var logEntry = new LogRecord
      {
        Args = logArgs,
        LogLevel = logLevel,
        Template = template,
        Exception = exception
      };

      _cache.Set(key, logEntry, GetEntryOptions(key));
    }

    _logger.Log(logLevel, exception, template, logArgs);
  }

  public void LogErrorDeduped(
    string template,
    [CallerFilePath] string callerFilePath = "",
    [CallerLineNumber] int callerLineNumber = 0,
    [CallerMemberName] string callerMemberName = "",
    Exception? exception = null,
    params object?[] args)
  {
    LogDeduped(
      LogLevel.Error,
      template,
      callerFilePath,
      callerLineNumber,
      callerMemberName,
      exception,
      args);
  }

  public void LogInformationDeduped(
    string template,
    [CallerFilePath] string callerFilePath = "",
    [CallerLineNumber] int callerLineNumber = 0,
    [CallerMemberName] string callerMemberName = "",
    Exception? exception = null,
    params object?[] args)
  {
    LogDeduped(
      LogLevel.Information,
      template,
      callerFilePath,
      callerLineNumber,
      callerMemberName,
      exception,
      args);
  }

  public void LogWarningDeduped(
    string template,
    [CallerFilePath] string callerFilePath = "",
    [CallerLineNumber] int callerLineNumber = 0,
    [CallerMemberName] string callerMemberName = "",
    Exception? exception = null,
    params object?[] args)
  {
    LogDeduped(
      LogLevel.Warning,
      template,
      callerFilePath,
      callerLineNumber,
      callerMemberName,
      exception,
      args);
  }

  [SuppressMessage("Usage", "CA2254:Template should be a static expression")]
  private void FlushLogRecord(LogRecord logRecord)
  {
    if (logRecord.TimesLogged <= 0)
    {
      return;
    }

    var template = $"{logRecord.Template} (Repeated {logRecord.TimesLogged} time(s))";
    _logger.Log(logRecord.LogLevel, logRecord.Exception, template, logRecord.Args);
  }

  [SuppressMessage("Usage", "CA2254:Template should be a static expression")]
  private MemoryCacheEntryOptions GetEntryOptions(string key)
  {
    var entryOptions = new MemoryCacheEntryOptions
    {
      AbsoluteExpirationRelativeToNow = _cacheDuration,
    };

    entryOptions.PostEvictionCallbacks.Add(new()
    {
      EvictionCallback = (_, value, _, _) =>
      {
        if (_isDisposing || value is not LogRecord logRecord)
        {
          return;
        }

        FlushLogRecord(logRecord);
      }
    });

    return entryOptions;
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
