using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace ControlR.Libraries.Shared.Helpers;

public static class Debouncer
{
    private static readonly ConcurrentDictionary<object, System.Timers.Timer> _timers = new();

    public static void Debounce(
        TimeSpan wait,
        Action action,
        string key = "",
        [CallerMemberName] string callerMemberName = "",
        [CallerFilePath] string callerFilePath = "",
        Action<Exception>? exceptionHandler = null)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            key = $"{callerMemberName}-{callerFilePath}";
        }

        if (_timers.TryRemove(key, out var timer))
        {
            timer.Stop();
            timer.Dispose();
        }

        timer = new System.Timers.Timer(wait)
        {
            AutoReset = false
        };

        timer.Elapsed += (s, e) =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exceptionHandler?.Invoke(ex);
            }
            finally
            {
                if (_timers.TryGetValue(key, out var result))
                {
                    result?.Dispose();
                }
            }
        };
        _timers.TryAdd(key, timer);
        timer.Start();
    }

    public static void Debounce(
       TimeSpan wait,
       Func<Task> func,
       string key = "",
       [CallerMemberName] string callerMemberName = "",
       [CallerFilePath] string callerFilePath = "",
       Action<Exception>? exceptionHandler = null)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            key = $"{callerMemberName}-{callerFilePath}";
        }

        if (_timers.TryRemove(key, out var timer))
        {
            timer.Stop();
            timer.Dispose();
        }

        timer = new System.Timers.Timer(wait)
        {
            AutoReset = false
        };

        timer.Elapsed += async (s, e) =>
        {
            try
            {
                await func();
            }
            catch (Exception ex)
            {
                exceptionHandler?.Invoke(ex);
            }
            finally
            {
                if (_timers.TryGetValue(key, out var result))
                {
                    result?.Dispose();
                }
            }
        };
        _timers.TryAdd(key, timer);
        timer.Start();
    }
}
