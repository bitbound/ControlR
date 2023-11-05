using ControlR.Shared.Extensions;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace ControlR.Devices.Common.Services;

public class FileLogger(
    string _componentVersion,
    string _categoryName,
    Func<string> _logPathFactory,
    TimeSpan _logRetention) : ILogger
{
    private static readonly ConcurrentStack<string> _scopeStack = new();
    private static readonly SemaphoreSlim _writeLock = new(1, 1);
    private static readonly ConcurrentQueue<string> _writeQueue = new();

    private DateTimeOffset _lastLogCleanup;

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
    {
        _scopeStack.Push($"{state}");
        return new NoopDisposable();
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return logLevel switch
        {
#if DEBUG
            LogLevel.Trace or LogLevel.Debug => true,
#endif
            LogLevel.Information or LogLevel.Warning or LogLevel.Error or LogLevel.Critical => true,
            _ => false,
        };
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        try
        {
            var message = FormatLogEntry(logLevel, _categoryName, $"{state}", exception, [.. _scopeStack]);
            _writeQueue.Enqueue(message);

            DrainWriteQueue().AndForget();

            //CheckLogFileExists();
            //File.AppendAllText(LogPath, message);
            //CleanupLogs();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error queuing log entry: {ex.Message}");
        }
    }

    private void CheckLogFileExists()
    {
        var logPath = _logPathFactory();
        Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
        if (!File.Exists(logPath))
        {
            File.Create(logPath).Close();
        }
    }

    private void CleanupLogs()
    {
        if (DateTimeOffset.Now - _lastLogCleanup < TimeSpan.FromDays(1))
        {
            return;
        }

        _lastLogCleanup = DateTimeOffset.Now;

        var logFiles = Directory.GetFiles(Path.GetDirectoryName(_logPathFactory())!)
            .Select(x => new FileInfo(x))
            .Where(x => DateTime.Now - x.CreationTime > _logRetention);

        foreach (var file in logFiles)
        {
            try
            {
                file.Delete();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error while trying to delete log file {file.FullName}.  Message: {ex.Message}");
            }
        }
    }

    private async Task DrainWriteQueue()
    {
        if (!await _writeLock.WaitAsync(0))
        {
            return;
        }

        try
        {
            CheckLogFileExists();
            CleanupLogs();

            while (_writeQueue.TryDequeue(out var message))
            {
                File.AppendAllText(_logPathFactory(), message);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while trying to drain log queue.  Message: {ex.Message}");
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private string FormatLogEntry(LogLevel logLevel, string categoryName, string state, Exception? exception, string[] scopeStack)
    {
        var ex = exception;
        var exMessage = !string.IsNullOrWhiteSpace(exception?.Message) ?
            $"[{exception.GetType().Name}]  {exception.Message}" :
            null;

        while (ex?.InnerException is not null)
        {
            exMessage += $" | [{ex.InnerException.GetType().Name}]  {ex.InnerException.Message}";
            ex = ex.InnerException;
        }

        var entry =
            $"[{logLevel}]  " +
            $"[v{_componentVersion}]  " +
            $"[Process ID: {Environment.ProcessId}]  " +
            $"[Thread ID: {Environment.CurrentManagedThreadId}]  " +
            $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff}]  ";

        entry += scopeStack.Length != 0 ?
                    $"[{categoryName} => {string.Join(" => ", scopeStack)}]  " :
                    $"[{categoryName}]  ";

        entry += $"{state} ";

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

    private class NoopDisposable : IDisposable
    {
        public void Dispose()
        {
            _scopeStack.TryPop(out _);
        }
    }
}