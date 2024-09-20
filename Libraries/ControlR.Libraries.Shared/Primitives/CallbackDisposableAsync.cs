namespace ControlR.Libraries.Shared.Primitives;

public sealed class CallbackDisposableAsync(
  Func<Task> disposeCallback,
  Func<Exception, Task>? exceptionHandler = null) : IAsyncDisposable
{
  public async ValueTask DisposeAsync()
  {
    try
    {
      await disposeCallback();
    }
    catch (Exception ex)
    {
      if (exceptionHandler is not null)
      {
        await exceptionHandler.Invoke(ex);
      }
    }
  }
}