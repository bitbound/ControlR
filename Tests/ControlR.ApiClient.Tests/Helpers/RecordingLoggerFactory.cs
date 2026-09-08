using Microsoft.Extensions.Logging;

namespace ControlR.ApiClient.Tests.Helpers;

/// <summary>
/// A logger that appends every logged message to a thread-safe list. Used to assert on the
/// absence (or presence) of specific log outcomes, e.g. that eviction mid-refresh stays quiet.
/// </summary>
internal sealed class RecordingLoggerFactory : ILoggerFactory
{
  private readonly RecordingLogger _logger = new();

  public IReadOnlyList<string> Messages => _logger.Messages;

  public void AddProvider(ILoggerProvider provider)
  {
  }

  public ILogger CreateLogger(string categoryName) => _logger;

  public void Dispose()
  {
  }

  private sealed class RecordingLogger : ILogger
{
  private readonly Lock _lock = new();
  private readonly List<string> _messages = [];

  public IReadOnlyList<string> Messages
  {
    get
    {
      lock (_lock)
      {
        return [.. _messages];
      }
    }
  }

  public IDisposable? BeginScope<TState>(TState state)
      where TState : notnull => null;

  public bool IsEnabled(LogLevel logLevel) => true;

  public void Log<TState>(
    LogLevel logLevel,
    EventId eventId,
    TState state,
    Exception? exception,
    Func<TState, Exception?, string> formatter)
  {
    lock (_lock)
    {
      _messages.Add(formatter(state, exception));
    }
  }
  }
}
