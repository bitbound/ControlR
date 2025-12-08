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
///   Attempts to acquire a lock on the semaphore within the specified timeout.
/// </summary>
/// <param name="semaphore">
///   The semaphore on which to acquire the lock.
/// </param>
/// <param name="timeout">
///   The maximum time to wait for the lock.
/// </param>
/// <returns>
///   An <see cref="IDisposable"/> that releases the lock when disposed.
/// </returns>
/// <exception cref="TimeoutException">
///   Thrown if the lock could not be acquired within the specified timeout.
/// </exception>
  public static IDisposable AcquireLock(
    this SemaphoreSlim semaphore,
    TimeSpan timeout)
  {
    var acquired = semaphore.Wait(timeout);
    if (!acquired)
    {
      throw new TimeoutException("Failed to acquire the semaphore within the specified timeout.");
    }
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
  ///   Attempts to acquire a lock on the semaphore asynchronously within the specified timeout.
  /// </summary>
  /// <param name="semaphore">
  ///   The semaphore on which to acquire the lock.
  /// </param>
  /// <param name="timeout">
  ///   The maximum time to wait for the lock.
  /// </param>
  /// <returns>
  ///   An <see cref="IDisposable"/> that releases the lock when disposed.
  /// </returns>
  /// <exception cref="TimeoutException">
  ///   Thrown if the lock could not be acquired within the specified timeout.
  /// </exception>
  public static async Task<IDisposable> AcquireLockAsync(this SemaphoreSlim semaphore, TimeSpan timeout)
  {
    var acquired = await semaphore.WaitAsync(timeout).ConfigureAwait(false);
    if (!acquired)
    {
      throw new TimeoutException("Failed to acquire the semaphore within the specified timeout.");
    }
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
