namespace ControlR.Libraries.Shared.Services;

public interface IWaiter
{
  Task<bool> WaitFor(
      Func<bool> condition,
      TimeSpan? pollingDelay = null,
      Func<Task>? conditionFailedCallback = null,
      bool throwOnCancellation = false,
      CancellationToken cancellationToken = default);
}

public class Waiter(TimeProvider timeProvider) : IWaiter
{
  private readonly TimeProvider _timeProvider = timeProvider;
  public static Waiter Default { get; } = new(TimeProvider.System);

  public async Task<bool> WaitFor(
    Func<bool> condition,
    TimeSpan? pollingDelay = null,
    Func<Task>? conditionFailedCallback = null,
    bool throwOnCancellation = false,
    CancellationToken cancellationToken = default)
  {
    pollingDelay ??= TimeSpan.FromMilliseconds(100);

    while (!condition())
    {
      try
      {
        if (conditionFailedCallback is not null)
        {
          await conditionFailedCallback();
        }

        await Task.Delay(pollingDelay.Value, _timeProvider, cancellationToken);
      }
      catch (OperationCanceledException)
      {
        if (throwOnCancellation)
        {
          throw;
        }
        return false;
      }
    }
    return condition();
  }

}
