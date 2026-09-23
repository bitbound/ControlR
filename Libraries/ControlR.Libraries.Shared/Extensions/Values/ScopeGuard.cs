namespace ControlR.Libraries.Shared.Extensions.Values;

/// <summary>
/// Holds a disposable for the current scope and disposes it at scope end, unless dismissed first.
/// </summary>
/// <remarks>
/// Call <see cref="Dismiss"/> when the value should outlive the scope, typically right before
/// returning it. Otherwise the guarded value is disposed by <see cref="Dispose"/>, even on
/// exception paths.
/// </remarks>
/// <typeparam name="T">The type of the resource to guard. Must implement <see cref="IDisposable"/>.</typeparam>
/// <param name="value">The disposable resource held by the guard.</param>
public sealed class ScopeGuard<T>(T value) : IDisposable
  where T : IDisposable
{
  public bool IsDismissed { get; private set; }

  public T Value { get; } = value;

  /// <summary>
  /// Releases the guard so the value is not disposed. Returns the value for convenient hand-off.
  /// </summary>
  public T Dismiss()
  {
    IsDismissed = true;
    return Value;
  }

  public void Dispose()
  {
    if (IsDismissed)
    {
      return;
    }
    Value.Dispose();
  }
}
