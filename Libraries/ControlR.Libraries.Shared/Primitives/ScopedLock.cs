namespace ControlR.Libraries.Shared.Primitives;

/// <summary>
/// A scoped lock implementation for synchronization using the RAII pattern.
/// The lock is automatically released when the returned guard is disposed.
/// </summary>
/// <typeparam name="T">The type of value being protected.</typeparam>
public class ScopedLock<T>(T? value) : IDisposable where T : class?
{
  private readonly Lock _lock = new();

  private bool _disposedValue;
  private T? _value = value;

  public void Dispose()
  {
    // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
    Dispose(disposing: true);
    GC.SuppressFinalize(this);
  }

  /// <summary>
  /// Acquires the lock and returns a guard that will release it on disposal.
  /// </summary>
  /// <returns>A disposable guard containing the protected value.</returns>
  public ScopedGuard<T?> Lock()
  {
    _lock.Enter();
    return new ScopedGuard<T?>(() => _value, v => _value = v, _lock.Exit);
  }

  /// <summary>
  /// Attempts to acquire the lock with a timeout.
  /// </summary>
  /// <param name="timeout">The maximum time to wait for the lock.</param>
  /// <returns>A guard if the lock was acquired, or null if the timeout expired.</returns>
  public ScopedGuard<T?>? TryLock(TimeSpan timeout)
  {
    if (_lock.TryEnter(timeout))
    {
      return new ScopedGuard<T?>(() => _value, v => _value = v, _lock.Exit);
    }
    return null;
  }

  /// <summary>
  /// Attempts to acquire the lock immediately without blocking.
  /// </summary>
  /// <returns>A guard if the lock was acquired immediately, or null if it was not available.</returns>
  public ScopedGuard<T?>? TryLock()
  {
    if (_lock.TryEnter())
    {
      return new ScopedGuard<T?>(() => _value, SetValue, _lock.Exit);
    }
    return null;
  }

  protected virtual void Dispose(bool disposing)
  {
    if (!_disposedValue)
    {
      if (disposing)
      {
        if (_value is IAsyncDisposable asyncDisposable)
        {
          asyncDisposable.DisposeAsync().Forget();
        }
        else if (_value is IDisposable disposable)
        {
          disposable.Dispose();
        }
        _value = default;
      }

      _disposedValue = true;
    }
  }

  private void SetValue(T? value)
  {
    if (_value is IAsyncDisposable asyncDisposable)
    {
      asyncDisposable.DisposeAsync().Forget();
    }
    else if (_value is IDisposable disposable)
    {
      disposable.Dispose();
    }
    _value = value;
  }
}