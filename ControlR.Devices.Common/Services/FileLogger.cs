using ControlR.Shared.Services;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace ControlR.Devices.Common.Services;

public class FileLogger(string componentName, string componentVersion, string categoryName) : ILogger
{
    private static readonly ConcurrentStack<string> _scopeStack = new();
    private static readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly string _categoryName = categoryName;
    private readonly string _componentName = componentName;
    private readonly string _componentVersion = componentVersion;
    private DateTimeOffset _lastLogCleanup;

    private static string LogsFolderPath
    {
        get
        {
            if (OperatingSystem.IsWindows())
            {
                var logsPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "ControlR",
                    "Logs");

                if (EnvironmentHelper.Instance.IsDebug)
                {
                    logsPath += "_Debug";
                }
                return logsPath;
            }

            if (OperatingSystem.IsLinux())
            {
                if (EnvironmentHelper.Instance.IsDebug)
                {
                    return "/var/log/ControlR_debug";
                }
                return "/var/log/ControlR";
            }

            throw new PlatformNotSupportedException();
        }
    }
    private string LogPath => Path.Combine(LogsFolderPath, _componentName, $"LogFile_{DateTime.Now:yyyy-MM-dd}.log");
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
        _writeLock.Wait();

        try
        {

            var message = FormatLogEntry(logLevel, _categoryName, $"{state}", exception, [.. _scopeStack]);
            CheckLogFileExists();
            File.AppendAllText(LogPath, message);
            CleanupLogs();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error writing log entry: {ex.Message}");
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private void CheckLogFileExists()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
        if (!File.Exists(LogPath))
        {
            File.Create(LogPath).Close();
        }
    }

    private void CleanupLogs()
    {
        if (DateTimeOffset.Now - _lastLogCleanup < TimeSpan.FromDays(1))
        {
            return;
        }

        _lastLogCleanup = DateTimeOffset.Now;

        var logFiles = Directory.GetFiles(Path.GetDirectoryName(LogPath)!)
            .Select(x => new FileInfo(x))
            .Where(x => DateTime.Now - x.CreationTime > TimeSpan.FromDays(7));

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
    private string FormatLogEntry(LogLevel logLevel, string categoryName, string state, Exception? exception, string[] scopeStack)
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
            $"[v{_componentVersion}]\t" +
            $"[Process ID: {Environment.ProcessId}]\t" +
            $"[Thread ID: {Environment.CurrentManagedThreadId}]\t" +
            $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff}]\t";

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

    private class NoopDisposable : IDisposable
    {
        public void Dispose()
        {
            _scopeStack.TryPop(out _);
        }
    }
}
