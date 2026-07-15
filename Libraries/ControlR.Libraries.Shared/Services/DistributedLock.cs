using System.Collections.Concurrent;

namespace ControlR.Libraries.Shared.Services;


public interface IDistributedLock
{
  Task<IDisposable> AcquireLock(string key, CancellationToken cancellationToken);
}

public class DistributedLock : IDistributedLock
{
  private readonly ConcurrentDictionary<string, SemaphoreSlim> _semaphores = [];

  public async Task<IDisposable> AcquireLock(string key, CancellationToken cancellationToken)
  {
    var semaphore = _semaphores.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
    await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
    return new CallbackDisposable(() =>
    {
      semaphore.Release();
      if (semaphore.CurrentCount > 0)
      {
        _semaphores.TryRemove(new KeyValuePair<string, SemaphoreSlim>(key, semaphore));
      }
    });
  }
}