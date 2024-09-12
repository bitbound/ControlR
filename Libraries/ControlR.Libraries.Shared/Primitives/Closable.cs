using System.Collections.Concurrent;

namespace ControlR.Libraries.Shared.Primitives;

public interface IClosable
{
    Task InvokeOnClosed();
    IDisposable OnClosed(Func<Task> callback);
}

public class Closable(ILogger<Closable> _logger) : IClosable
{
    private readonly ConcurrentDictionary<Guid, Func<Task>> _onCloseCallbacks = new();

    public IDisposable OnClosed(Func<Task> callback)
    {
        var id = Guid.NewGuid();
        _onCloseCallbacks.TryAdd(id, callback);
        return new CallbackDisposable(() =>
        {
            _onCloseCallbacks.TryRemove(id, out _);
        });
    }

    public async Task InvokeOnClosed()
    {
        foreach (var callback in _onCloseCallbacks.Values)
        {
            try
            {
                await callback();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while executing on close callback.");
            }
        }
    }
}
