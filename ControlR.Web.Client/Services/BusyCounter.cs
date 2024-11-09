namespace ControlR.Web.Client.Services;

public interface IBusyCounter
{
  bool IsBusy { get; }

  int PendingOperations { get; }

  IDisposable IncrementBusyCounter(Action? additionalDisposedAction = null);
}

internal class BusyCounter(IMessenger messenger) : IBusyCounter
{
  private volatile int _busyCounter;

  public bool IsBusy => _busyCounter > 0;


  public int PendingOperations => _busyCounter;

  public IDisposable IncrementBusyCounter(Action? additionalDisposedAction = null)
  {
    Interlocked.Increment(ref _busyCounter);

    messenger.SendGenericMessage(EventMessageKind.PendingOperationsChanged);

    return new CallbackDisposable(() =>
    {
      Interlocked.Decrement(ref _busyCounter);
      messenger.SendGenericMessage(EventMessageKind.PendingOperationsChanged);

      additionalDisposedAction?.Invoke();
    });
  }
}