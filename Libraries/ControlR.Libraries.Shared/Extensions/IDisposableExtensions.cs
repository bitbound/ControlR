using ControlR.Libraries.Shared.Extensions.Values;

namespace ControlR.Libraries.Shared.Extensions;

public static class IDisposableExtensions
{
  /// <summary>
  ///   Wraps the specified disposable object in a <see cref="MaybeDisposable{T}"/> instance.
  /// </summary>
  /// <remarks>
  ///   This method enables fluent usage of <see cref="MaybeDisposable{T}"/> for any object that implements
  ///   IDisposable. The returned <see cref="MaybeDisposable{T}"/> can be used to manage the lifetime of the 
  ///   wrapped object in scenarios where conditional disposal is required.
  /// </remarks>
  /// <typeparam name="T">
  ///   The type of the disposable object to wrap. Must implement IDisposable.
  /// </typeparam>
  /// <param name="disposable">
  ///   The disposable object to wrap. Cannot be null.
  /// </param>
  /// <returns>
  ///   A <see cref="MaybeDisposable{T}"/> instance that encapsulates the specified disposable object.
  /// </returns>
  public static MaybeDisposable<T> AsMaybeDisposable<T>(this T disposable)
    where T : IDisposable
  {
    return new MaybeDisposable<T>(disposable);
  }
}
