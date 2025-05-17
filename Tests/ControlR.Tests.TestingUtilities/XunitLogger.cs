using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using Xunit.Abstractions;

namespace ControlR.Tests.TestingUtilities;

public class XunitLogger<T>(ITestOutputHelper testOutput) : XunitLogger(testOutput, nameof(T)), ILogger<T>
{
}

public class XunitLogger(ITestOutputHelper testOutput, string categoryName) : ILogger
{
  private readonly ConcurrentStack<string> _scopeStack = new();
  private readonly string _categoryName = categoryName;

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

  private static string FormatLogEntry(LogLevel logLevel, string categoryName, string state, Exception? exception, string[] scopeStack)
  {
    var ex = exception;
    var exMessage = exception?.Message;

    while (ex?.InnerException is not null)
    {
      exMessage += $" | {ex.InnerException.Message}";
      ex = ex.InnerException;
    }

    var entry =
        $"[{logLevel}]\t" +
        $"[Thread ID: {Environment.CurrentManagedThreadId}]\t" +
        $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff}]\t";

    if (exception is not null)
    {
      entry += $"[Exception: {exception.GetType().Name}]\t";
    }

    entry += scopeStack.Length != 0 ?
                $"[{categoryName} => {string.Join(" => ", scopeStack)}]\t" :
                $"[{categoryName}]\t";

    entry += $"{state}\t";

    if (!string.IsNullOrWhiteSpace(exMessage))
    {
      entry += exMessage;
    }

    if (exception is not null)
    {
      entry += $"{Environment.NewLine}{exception.StackTrace}";
    }

    entry += Environment.NewLine;

    return entry;
  }

  private class ScopeDisposable(ConcurrentStack<string> scopeStack) : IDisposable
  {
    public void Dispose()
    {
      scopeStack.TryPop(out _);
    }
  }
}
