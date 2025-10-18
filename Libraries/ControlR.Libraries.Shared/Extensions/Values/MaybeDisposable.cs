using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ControlR.Libraries.Shared.Extensions.Values;

/// <summary>
///   Provides a disposable wrapper that allows suppressing disposal of the underlying resource.
/// </summary>
/// <remarks>
///   If <see cref="Suppress"/> is called before <see cref="Dispose"/>, disposal of the underlying resource is
///   skipped. This can be useful in scenarios where cleanup should be conditionally suppressed.
/// </remarks>
/// <typeparam name="T">
///   The type of the resource to wrap. Must implement <see cref="IDisposable"/>.
/// </typeparam>
/// <param name="value">
///   The disposable resource to be managed by the wrapper.
/// </param>
public sealed class MaybeDisposable<T>(T value) : IDisposable
  where T : IDisposable
{
  public bool IsSuppressed { get; private set; }

  public T Value { get; } = value;

  public void Dispose()
  {
    if (IsSuppressed)
    {
      return;
    }
    Value.Dispose();
  }

  /// <summary>
  /// Restores the disposal state, so the underlying value will be disposed, and returns the current value.
  /// </summary>
  /// <returns>The value of type <typeparamref name="T"/> associated with this instance.</returns>
  public T Restore()
  {
    IsSuppressed = false;
    return Value;
  }

  /// <summary>
  ///   Suppresses the disposal of the underlying resource and returns the current value.
  /// </summary>
  /// <returns>The value of type <typeparamref name="T"/> associated with the aborted operation.</returns>
  public T Suppress()
  {
    IsSuppressed = true;
    return Value;
  }
}
