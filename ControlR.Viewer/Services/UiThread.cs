namespace ControlR.Viewer.Services;

public interface IUiThread
{
    Task InvokeAsync(Func<Task> func);
    Task InvokeAsync(Action action);
    Task<T> InvokeAsync<T>(Func<Task<T>> func);
}

internal class UiThread : IUiThread
{
    public async Task InvokeAsync(Func<Task> func)
    {
        if (MainThread.IsMainThread)
        {
            await func.Invoke();
        }
        else
        {
            await MainThread.InvokeOnMainThreadAsync(func);
        }
    }

    public async Task<T> InvokeAsync<T>(Func<Task<T>> func)
    {
        if (MainThread.IsMainThread)
        {
            return await func.Invoke();
        }
        else
        {
            return await MainThread.InvokeOnMainThreadAsync(func);
        }
    }

    public async Task InvokeAsync(Action action)
    {
        if (MainThread.IsMainThread)
        {
            action.Invoke();
        }
        else
        {
            await MainThread.InvokeOnMainThreadAsync(action);
        }
    }
}
