using System.Diagnostics;

namespace ControlR.Libraries.Shared.Primitives;
public sealed class DisposableValue<T>(
    T _value,
    Action? _disposeCallback = null) : IDisposable
{
    private readonly Action? _disposeCallback = _disposeCallback;

    public T Value { get; } = _value;

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
