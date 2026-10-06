namespace ControlR.Libraries.Shared.Services.Locks;

/// <summary>
/// Serializes access to a named critical section. Implementations may be in-process
/// (single instance) or distributed (e.g. Redis); call sites remain agnostic.
/// </summary>
public interface IAsyncLock
{
  /// <summary>
  /// Acquires the named lock, waiting until it is available or the token is cancelled.
  /// Dispose the returned handle (via <c>await using</c>) to release the lock.
  /// </summary>
  Task<IAsyncDisposable> AcquireAsync(string key, CancellationToken cancellationToken);
}
