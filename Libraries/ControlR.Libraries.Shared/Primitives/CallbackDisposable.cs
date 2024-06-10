namespace ControlR.Libraries.Shared.Primitives;

public sealed class CallbackDisposable(Action _disposeCallback) : IDisposable
{
    private readonly Action _disposeCallback = _disposeCallback;

    public void Dispose()
    {
        try
        {
            _disposeCallback();
        }
        catch { }
    }
}
