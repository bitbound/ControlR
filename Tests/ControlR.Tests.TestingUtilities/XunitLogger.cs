using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text;
using Xunit.Abstractions;

namespace ControlR.Tests.TestingUtilities;

public class XunitLogger<T>(ITestOutputHelper testOutput) : XunitLogger(testOutput, nameof(T)), ILogger<T>
{
}

public class XunitLogger(ITestOutputHelper testOutput, string categoryName) : ILogger
{
  private readonly string _categoryName = categoryName;
  private readonly ConcurrentStack<string> _scopeStack = new();

  public IDisposable BeginScope<TState>(TState state) where TState : notnull
  {
    _scopeStack.Push($"{state}");
    return new ScopeDisposable(_scopeStack);
  }

  public bool IsEnabled(LogLevel logLevel)
  {
    return true;
  }

  public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
  {
    var entry = FormatLogEntry(logLevel, _categoryName, $"{state}", exception, [.. _scopeStack]);
    testOutput.WriteLine(entry);
  }

  private string FormatLogEntry(LogLevel logLevel, string category, string state, Exception? exception,
    string[] scopeStack)
  {
    var ex = exception;
    var exMessage = !string.IsNullOrWhiteSpace(exception?.Message)
      ? $"[{exception.GetType().Name}]  {exception.Message}"
      : null;

    while (ex?.InnerException is not null)
    {
      exMessage += $" | [{ex.InnerException.GetType().Name}]  {ex.InnerException.Message}";
      ex = ex.InnerException;
    }

    var entry = new StringBuilder();

    entry.Append(
      $"[{logLevel}]  " +
      $"[Process ID: {Environment.ProcessId}]  " +
      $"[Thread ID: {Environment.CurrentManagedThreadId}]  " +
      $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff}]  ");

    entry.Append(scopeStack.Length != 0
      ? $"[{category} => {string.Join(" => ", scopeStack)}]  "
      : $"[{category}]  ");

    entry.Append($"{state} ");

    if (!string.IsNullOrWhiteSpace(exMessage))
    {
      entry.Append(exMessage);
    }

    if (exception is not null)
    {
      entry.Append($"{Environment.NewLine}{exception.StackTrace}");
    }

    return entry.ToString();
  }

  private class ScopeDisposable(ConcurrentStack<string> scopeStack) : IDisposable
  {
    public void Dispose()
    {
      scopeStack.TryPop(out _);
    }
  }
}
