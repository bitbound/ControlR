using ControlR.Libraries.Shared.Collections;
using System.Collections.Concurrent;

namespace ControlR.Libraries.Shared.Primitives;

public interface IClosable
{
  Task InvokeOnClosed();
  IDisposable OnClosed(Func<Task> callback);
}

public class Closable(ILogger<Closable> logger) : IClosable
{
  private readonly ConcurrentList<Func<Task>> _onCloseCallbacks = [];

  public IDisposable OnClosed(Func<Task> callback)
  {
    _onCloseCallbacks.Add(callback);
    return new CallbackDisposable(() => { _onCloseCallbacks.Remove(callback); });
  }

  public async Task InvokeOnClosed()
  {
    foreach (var callback in _onCloseCallbacks)
    {
      try
      {
        await callback();
      }
      catch (Exception ex)
      {
        logger.LogError(ex, "Error while executing on close callback.");
      }
    }
  }
}