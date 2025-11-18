using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Serilog.Events;

namespace ControlR.Libraries.DevicesCommon.Services;

public class SerilogLogger<T> : ILogger<T>
{
  private static readonly ConcurrentStack<string> _scopeStack = new();

  public IDisposable? BeginScope<TState>(TState state) where TState : notnull
  {
    try
    {
      _scopeStack.Push(state?.ToString() ?? string.Empty);
    }
    catch
    {
      // ignore scope push failures
    }

    return new ScopeDisposable();
  }

  public bool IsEnabled(LogLevel logLevel)
  {
    var level = ToSerilogLevel(logLevel);
    if (level == null) return false;
    return global::Serilog.Log.IsEnabled(level.Value);
  }

  public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
  {
    if (formatter == null) return;

    var rendered = formatter(state, exception) ?? string.Empty;
    if (string.IsNullOrEmpty(rendered) && exception == null)
    {
      return;
    }

    var level = ToSerilogLevel(logLevel) ?? LogEventLevel.Information;

    // Build a category + scopes prefix similar to FileLogger
    var category = typeof(T).FullName ?? typeof(T).Name;
    var scopes = _scopeStack.ToArray();
    string prefix = scopes.Length != 0
      ? $"[{category} => {string.Join(" => ", scopes.Reverse())}] "
      : $"[{category}] ";

    var message = prefix + rendered;

    switch (level)
    {
      case LogEventLevel.Verbose:
        global::Serilog.Log.Verbose(exception, "{Message}", message);
        break;
      case LogEventLevel.Debug:
        global::Serilog.Log.Debug(exception, "{Message}", message);
        break;
      case LogEventLevel.Information:
        global::Serilog.Log.Information(exception, "{Message}", message);
        break;
      case LogEventLevel.Warning:
        global::Serilog.Log.Warning(exception, "{Message}", message);
        break;
      case LogEventLevel.Error:
        global::Serilog.Log.Error(exception, "{Message}", message);
        break;
      case LogEventLevel.Fatal:
        global::Serilog.Log.Fatal(exception, "{Message}", message);
        break;
      default:
        global::Serilog.Log.Information(exception, "{Message}", message);
        break;
    }
  }


  private static LogEventLevel? ToSerilogLevel(LogLevel level)
  {
    return level switch
    {
      LogLevel.Trace => LogEventLevel.Verbose,
      LogLevel.Debug => LogEventLevel.Debug,
      LogLevel.Information => LogEventLevel.Information,
      LogLevel.Warning => LogEventLevel.Warning,
      LogLevel.Error => LogEventLevel.Error,
      LogLevel.Critical => LogEventLevel.Fatal,
      LogLevel.None => null,
      _ => LogEventLevel.Information,
    };
  }


  private sealed class ScopeDisposable : IDisposable
  {
    public void Dispose()
    {
      _scopeStack.TryPop(out _);
    }
  }

}