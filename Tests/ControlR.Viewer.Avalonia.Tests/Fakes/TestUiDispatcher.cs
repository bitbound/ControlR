using Avalonia.Threading;
using ControlR.Libraries.Avalonia.Services;

namespace ControlR.Viewer.Avalonia.Tests.Fakes;

public sealed class TestUiDispatcher : IUiDispatcher
{
  public bool CheckAccess() => true;

  public void Invoke(Action action) => action();

  public T Invoke<T>(Func<T> func) => func();

  public Task InvokeAsync(Action action, DispatcherPriority priority)
  {
    action();
    return Task.CompletedTask;
  }

  public async Task InvokeAsync(Func<Task> callback, DispatcherPriority priority)
  {
    await callback();
  }

  public async Task<T> InvokeAsync<T>(Func<Task<T>> callback, DispatcherPriority priority)
  {
    return await callback();
  }

  public async Task InvokeAsync(Func<Task> func)
  {
    await func();
  }

  public async Task<T> InvokeAsync<T>(Func<Task<T>> func)
  {
    return await func();
  }

  public void Post(Action action, DispatcherPriority priority)
  {
    action();
  }

  public void Post(Action action)
  {
    action();
  }
}
