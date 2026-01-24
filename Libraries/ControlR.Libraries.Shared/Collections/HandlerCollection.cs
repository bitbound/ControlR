using System.Runtime.CompilerServices;

namespace ControlR.Libraries.Shared.Collections;

/// <summary>
/// Represents a collection of asynchronous handlers that can be dynamically added and invoked for a specified data
/// type. Supports associating handlers with subscribers and provides optional exception handling during invocation.
/// </summary>
public class HandlerCollection<T>(Func<Exception, Task>? exceptionHandler = null)
  where T : class
{
  private readonly Func<Exception, Task>? _exceptionHandler = exceptionHandler;
  private readonly ConditionalWeakTable<object, Func<T, Task>> _handlers = [];

  public IDisposable AddHandler(object subscriber, Func<T, Task> handler)
  {
    _handlers.Add(subscriber, handler);
    return new CallbackDisposable(() => _handlers.Remove(subscriber));
  }

  public async Task InvokeHandlers(T dataItem, CancellationToken cancellationToken)
  {
    var handlers = GetMessageHandlers();

    foreach (var handler in handlers)
    {
      try
      {
        if (cancellationToken.IsCancellationRequested)
        {
          break;
        }

        await handler(dataItem);
      }
      catch (Exception ex)
      {
        if (_exceptionHandler is not null)
        {
          await _exceptionHandler(ex);
        }
      }
    }
  }

  private List<Func<T, Task>> GetMessageHandlers()
  {
    return [.. _handlers.Select(x => x.Value)];
  }
}
