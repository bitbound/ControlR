using Microsoft.Extensions.Options;

namespace ControlR.Libraries.Shared.Helpers;

public class OptionsMonitorWrapper<T>(T options) : IOptionsMonitor<T>
{
  private readonly List<Action<T, string?>> _listeners = [];

  private T _options = options;

  public T CurrentValue => _options;

  public T Get(string? name) => _options;

  public IDisposable OnChange(Action<T, string?> listener)
  {
    lock (_listeners)
    {
      _listeners.Add(listener);
    }

    return new CallbackDisposable(() =>
    {
      lock (_listeners)
      {
        _listeners.Remove(listener);
      }
    });
  }

  public void Update(T options, string? name)
  {
    _options = options;
    lock (_listeners)
    {
      foreach (var listener in _listeners)
      {
        listener.Invoke(options, name);
      }
    }
  }
}
