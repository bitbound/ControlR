using ControlR.Libraries.Shared.Extensions.Values;

namespace ControlR.Libraries.Shared.Extensions;

public static class IDisposableExtensions
{
  /// <summary>
  /// Guards the disposable for the current scope with a <see cref="ScopeGuard{T}"/>.
  /// </summary>
  /// <remarks>
  /// The value is disposed at scope end unless <c>Dismiss()</c> is called first. Use this to create
  /// a disposable inside a scope and conditionally return it (see the guarded value via
  /// <c>Value</c>) without leaking on early-return or exception paths.
  /// </remarks>
  public static ScopeGuard<T> Guard<T>(this T disposable)
    where T : IDisposable
  {
    ArgumentNullException.ThrowIfNull(disposable);
    return new ScopeGuard<T>(disposable);
  }

  /// <summary>
  /// Attempts to dispose the specified <see cref="IDisposable"/> object, suppressing any exceptions that may occur during disposal.
  /// </summary>
  /// <param name="disposable">The <see cref="IDisposable"/> object to dispose. Can be null.</param>
  public static void TryDispose(this IDisposable? disposable)
  {
    try
    {
      disposable?.Dispose();
    }
    catch
    {
      // Suppress any exceptions thrown during disposal to prevent disruption of application flow.
    }
  }
}
