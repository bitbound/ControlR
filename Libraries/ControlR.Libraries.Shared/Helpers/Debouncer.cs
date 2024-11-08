using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Timer = System.Timers.Timer;

namespace ControlR.Libraries.Shared.Helpers;

public static class Debouncer
{
  private static readonly ConcurrentDictionary<object, Timer> _timers = new();

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

    timer = new Timer(wait)
    {
      AutoReset = false
    };

    timer.Elapsed += (_, _) =>
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

    timer = new Timer(wait)
    {
      AutoReset = false
    };

    timer.Elapsed += async (_, _) =>
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