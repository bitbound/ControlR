namespace ControlR.Web.Client.Services.DeviceAccess;

public interface IStateBase
{
  Task NotifyStateChanged();
  IDisposable OnStateChanged(Func<Task> callback);
}
public abstract class StateBase(ILogger<StateBase> logger) : IStateBase
{
  private readonly ConcurrentList<Func<Task>> _changeHandlers = [];
  private readonly ILogger<StateBase> _logger = logger;

  public virtual Task NotifyStateChanged()
  {
    return InvokeChangeHandlers();
  }

  public virtual IDisposable OnStateChanged(Func<Task> callback)
  {
    _changeHandlers.Add(callback);
    return new CallbackDisposable(() =>
    {
      _changeHandlers.Remove(callback);
    });
  }

  private async Task InvokeChangeHandlers()
  {
    foreach (var handler in _changeHandlers)
    {
      try
      {
        await handler();
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error occurred while invoking change handler.");
      }
    }
  }
}