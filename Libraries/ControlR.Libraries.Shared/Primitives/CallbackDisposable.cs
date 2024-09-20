namespace ControlR.Libraries.Shared.Primitives;

public sealed class CallbackDisposable(
  Action disposeCallback,
  Action<Exception>? exceptionHandler = null) : IDisposable
{
  public void Dispose()
  {
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
