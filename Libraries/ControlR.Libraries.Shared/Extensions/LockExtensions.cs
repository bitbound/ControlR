namespace ControlR.Libraries.Shared.Extensions;

public static class LockExtensions
{
  public static async Task<IDisposable> AcquireLock(
    this SemaphoreSlim semaphore,
    CancellationToken cancellationToken)
  {
    await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
    return new CallbackDisposable(() =>
    {
      semaphore.Release();
    });
  }
}
