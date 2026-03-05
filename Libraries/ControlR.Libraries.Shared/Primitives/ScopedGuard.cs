namespace ControlR.Libraries.Shared.Primitives;

/// <summary>
/// A disposable guard that releases a lock when disposed.
/// </summary>
/// <typeparam name="T">The type of value being protected.</typeparam>
public sealed class ScopedGuard<T>(Func<T?> valueGetter, Action<T?> valueSetter, Action onDispose) : IDisposable
  where T : class?
{
  private bool _disposed;

  /// <summary>
  /// Gets the protected value.
  /// </summary>
  public T? Value
  {
    get
    {
      ObjectDisposedException.ThrowIf(_disposed, nameof(ScopedGuard<>));
      return valueGetter();
    }
  }

  /// <summary>
  /// Releases the lock.
  /// </summary>
  public void Dispose()
  {
    if (!_disposed)
    {
      _disposed = true;
      onDispose();
    }
  }

  /// <summary>
  ///   Sets the protected value contained in the <see cref="ScopedLock{T}"/> that created this guard.
  ///   If the current value is disposable, it will be disposed before being replaced.
  /// </summary>
  /// <param name="value">
  ///   The new value to set.
  /// </param>
  public void SetValue(T? value)
  {
    ObjectDisposedException.ThrowIf(_disposed, nameof(ScopedGuard<>));
    valueSetter(value);
  }
}
