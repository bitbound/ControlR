namespace ControlR.Libraries.Shared.Services;

public interface IDelayer
{
  Task Delay(TimeSpan delay, CancellationToken cancellationToken = default);
  Task<bool> WaitForAsync(
      Func<bool> condition,
      TimeSpan? pollingDelay = null,
      Func<Task>? conditionFailedCallback = null,
      bool throwOnCancellation = false,
      CancellationToken cancellationToken = default);
}

public class Delayer(TimeProvider timeProvider) : IDelayer
{
  private readonly TimeProvider _timeProvider = timeProvider;
  public static Delayer Default { get; } = new(TimeProvider.System);

  public async Task<bool> WaitForAsync(
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

  public async Task Delay(TimeSpan delay, CancellationToken cancellationToken = default)
  {
    await Task.Delay(delay, _timeProvider, cancellationToken);
  }
}
