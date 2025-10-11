namespace ControlR.Libraries.Shared.Extensions;

public static class LockExtensions
{

  public static IDisposable AcquireLock(
    this SemaphoreSlim semaphore,
    CancellationToken cancellationToken)
  {
    semaphore.Wait(cancellationToken);
    return new CallbackDisposable(() =>
    {
      semaphore.Release();
    });
  }
  
  public static async Task<IDisposable> AcquireLockAsync(
    this SemaphoreSlim semaphore,
    CancellationToken cancellationToken)
  {
    await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
    return new CallbackDisposable(() =>
    {
      semaphore.Release();
    });
  }

  public static IDisposable Lock(this object lockTarget)
  {
    Monitor.Enter(lockTarget);
    return new CallbackDisposable(() =>
    {
      Monitor.Exit(lockTarget);
    });
  }
}
