namespace ControlR.Libraries.Shared.Primitives;

public sealed class CallbackDisposable(
  Action disposeCallback,
  Action<Exception>? exceptionHandler = null) : IDisposable
{
  private int _disposed;
  public void Dispose()
  {
    if (Interlocked.Exchange(ref _disposed, 1) == 1) return;

    try
    {
      disposeCallback();
    }
    catch (Exception ex)
    {
      exceptionHandler?.Invoke(ex);
    }
  }
}
