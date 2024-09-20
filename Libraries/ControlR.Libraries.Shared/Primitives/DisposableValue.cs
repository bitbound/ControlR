using System.Diagnostics;

namespace ControlR.Libraries.Shared.Primitives;

public sealed class DisposableValue<T>(
  T value,
  Action? disposeCallback = null) : IDisposable
{
  private readonly Action? _disposeCallback = disposeCallback;

  public T Value { get; } = value;

  public void Dispose()
  {
    try
    {
      _disposeCallback?.Invoke();
    }
    catch (Exception ex)
    {
      Debug.WriteLine
      ($"Error while invoking callback in {nameof(DisposableValue<T>)}.  " +
       $"Exception: {ex}");
    }
  }
}