using System.Collections.Concurrent;

namespace ControlR.Libraries.Shared.Services;

public interface IDistributedLock
{
  Task<IDisposable> AcquireLock(string key, CancellationToken cancellationToken);
}

public sealed class DistributedLock : IDistributedLock
{
  /// <summary>
  /// Maximum count is bound by manually-defined keys, so we don't have to worry about unbound growth.
  /// </summary>
  private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = [];

  public async Task<IDisposable> AcquireLock(string key, CancellationToken cancellationToken)
  {
    var semaphore = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
    return await semaphore.AcquireLockAsync(cancellationToken);
  }
}