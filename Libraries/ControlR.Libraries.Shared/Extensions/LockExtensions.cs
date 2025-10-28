namespace ControlR.Libraries.Shared.Extensions;

public static class LockExtensions
{

  /// <summary>
  ///   Attempts to acquire a lock on the semaphore.
  /// </summary>
  /// <param name="semaphore">
  ///   The semaphore on which to acquire the lock.
  /// </param>
  /// <param name="cancellationToken">
  ///   The cancellation token to observe while waiting for the lock.
  /// </param>
  /// <returns>
  ///   An <see cref="IDisposable"/> that releases the lock when disposed.
  /// </returns>
  /// <exception cref="OperationCanceledException">
  ///   Thrown if the operation is canceled via the provided <paramref name="cancellationToken"/>.
  /// </exception>
  /// <exception cref="ObjectDisposedException">
  /// Thrown if the semaphore has been disposed.
  /// </exception>
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

  /// <summary>
  ///   Attempts to acquire a lock on the semaphore asynchronously.
  /// </summary>
  /// <param name="semaphore">
  ///   The semaphore on which to acquire the lock.
  /// </param>
  /// <param name="cancellationToken">
  ///   The cancellation token to observe while waiting for the lock.
  /// </param>
  /// <returns>
  ///   An <see cref="IDisposable"/> that releases the lock when disposed.
  /// </returns>
  /// <exception cref="OperationCanceledException">
  ///   Thrown if the operation is canceled via the provided <paramref name="cancellationToken"/>.
  /// </exception>
  /// <exception cref="ObjectDisposedException">
  ///   Thrown if the semaphore has been disposed.
  /// </exception>
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

  /// <summary>
  ///   Acquires a lock on the specified target object.
  /// </summary>
  /// <param name="lockTarget">
  ///   The target object to lock.
  /// </param>
  /// <returns>
  ///   An <see cref="IDisposable"/> that releases the lock when disposed.
  /// </returns>
  public static IDisposable Lock(this object lockTarget)
  {
    Monitor.Enter(lockTarget);
    return new CallbackDisposable(() =>
    {
      Monitor.Exit(lockTarget);
    });
  }
}
