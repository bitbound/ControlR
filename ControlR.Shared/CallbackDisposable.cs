namespace ControlR.Shared;

public sealed class CallbackDisposable(Action disposeCallback) : IDisposable
{
    private readonly Action _disposeCallback = disposeCallback;

    public void Dispose()
    {
        try
        {
            _disposeCallback();
        }
        catch { }
    }
}
