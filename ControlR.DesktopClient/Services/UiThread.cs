namespace ControlR.DesktopClient.Services;

public class UiThread : IUiThread
{

  public void Invoke(Action action)
  {
    Dispatcher.UIThread.Invoke(action);
  }

  public T Invoke<T>(Func<T> func)
  {
    return Dispatcher.UIThread.Invoke(func);
  }

  public Task InvokeAsync(Func<Task> func)
  {
    return Dispatcher.UIThread.InvokeAsync(func);
  }

  public Task<T> InvokeAsync<T>(Func<Task<T>> func)
  {
    return Dispatcher.UIThread.InvokeAsync(func);
  }

  public void Post(Action action)
  {
    Dispatcher.UIThread.Post(action);
  }
}