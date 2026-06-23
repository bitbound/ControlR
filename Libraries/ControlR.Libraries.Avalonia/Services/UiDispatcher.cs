using Avalonia.Threading;

namespace ControlR.Libraries.Avalonia.Services;

public interface IUiDispatcher
{
  bool CheckAccess();

  void Invoke(Action action);

  T Invoke<T>(Func<T> func);

  Task InvokeAsync(Action action, DispatcherPriority priority);

  Task InvokeAsync(Func<Task> callback, DispatcherPriority priority);

  Task<T> InvokeAsync<T>(Func<Task<T>> callback, DispatcherPriority priority);

  Task InvokeAsync(Func<Task> func);

  Task<T> InvokeAsync<T>(Func<Task<T>> func);

  void Post(Action action, DispatcherPriority priority);

  void Post(Action action);
}

public sealed class UiDispatcher : IUiDispatcher
{
  public bool CheckAccess() => Dispatcher.UIThread.CheckAccess();

  public void Invoke(Action action) => Dispatcher.UIThread.Invoke(action);

  public T Invoke<T>(Func<T> func) => Dispatcher.UIThread.Invoke(func);

  public Task InvokeAsync(Action action, DispatcherPriority priority)
    => Dispatcher.UIThread.InvokeAsync(() => { action(); return Task.CompletedTask; }, priority);

  public Task InvokeAsync(Func<Task> callback, DispatcherPriority priority)
    => Dispatcher.UIThread.InvokeAsync(callback, priority);

  public Task<T> InvokeAsync<T>(Func<Task<T>> callback, DispatcherPriority priority)
    => Dispatcher.UIThread.InvokeAsync(callback, priority);

  public Task InvokeAsync(Func<Task> func) => Dispatcher.UIThread.InvokeAsync(func);

  public Task<T> InvokeAsync<T>(Func<Task<T>> func) => Dispatcher.UIThread.InvokeAsync(func);

  public void Post(Action action, DispatcherPriority priority)
    => Dispatcher.UIThread.Post(action, priority);

  public void Post(Action action) => Dispatcher.UIThread.Post(action);
}
