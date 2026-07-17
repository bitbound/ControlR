using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace ControlR.Libraries.TestingUtilities.Logging;

/// <summary>
/// Captures all log entries written by any logger for assertion in tests.
/// </summary>
public sealed class CapturingLoggerProvider : ILoggerProvider
{
  private readonly ConcurrentQueue<CapturedLog> _logs = new();

  public IReadOnlyList<CapturedLog> Logs => _logs.ToList();

  public ILogger CreateLogger(string categoryName)
  {
    return new CapturingLogger(this, categoryName);
  }

  public void Dispose()
  {
    GC.SuppressFinalize(this);
  }

  internal void Enqueue(LogLevel logLevel, string categoryName, string message, Exception? ex = null)
  {
    _logs.Enqueue(new CapturedLog(logLevel, categoryName, message, ex?.Message));
  }

  internal sealed class CapturingLogger(CapturingLoggerProvider provider, string categoryName) : ILogger
  {
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
      Func<TState, Exception?, string> formatter)
    {
      provider.Enqueue(logLevel, categoryName, formatter(state, exception), exception);
    }
  }
}