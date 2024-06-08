using ControlR.Libraries.Shared.Collections;
using ControlR.Libraries.Shared.Primitives;
using Microsoft.Extensions.Options;

namespace ControlR.Server.Tests.TestableServices;

public class TestableOptionsMonitor<T> : IOptionsMonitor<T>
    where T : new()
{
    private readonly ConcurrentList<Action<T, string?>> _listeners = [];

    private T _currentValue = new();
    public T CurrentValue => _currentValue;

    public void Set(T value)
    {
        _currentValue = value;
    }

    public T Get(string? name)
    {
        return _currentValue;
    }

    public IDisposable? OnChange(Action<T, string?> listener)
    {
        _listeners.Add(listener);
        return new CallbackDisposable(() =>
        {
            _listeners.Remove(listener);
        });
    }
}