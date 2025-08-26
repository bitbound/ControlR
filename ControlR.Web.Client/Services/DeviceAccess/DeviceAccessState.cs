namespace ControlR.Web.Client.Services.DeviceAccess;

public interface IDeviceAccessState
{
  DeviceDto CurrentDevice { get; set; }
  DeviceDto? CurrentDeviceMaybe { get; }
  bool IsDeviceLoaded { get; }
  IDisposable OnDeviceStateChanged(Func<Task> callback);
}

internal class DeviceAccessState(ILogger<DeviceAccessState> logger) : IDeviceAccessState
{
  private readonly ILogger<DeviceAccessState> _logger = logger;
  private readonly ConcurrentList<Func<Task>> _changeHandlers = [];
  private DeviceDto? _currentDevice;

  public DeviceDto CurrentDevice
  {
    get => _currentDevice ?? throw new InvalidOperationException("CurrentDevice is not set.");
    set
    {
      _currentDevice = value;
      InvokeChangeHandlers().Forget();
    }
  }

  public DeviceDto? CurrentDeviceMaybe => _currentDevice;

  public bool IsDeviceLoaded => _currentDevice != null;

  public IDisposable OnDeviceStateChanged(Func<Task> callback)
  {
    _changeHandlers.Add(callback);
    return new CallbackDisposable(() =>
    {
      _changeHandlers.Remove(callback);
    });
  }

  private async Task InvokeChangeHandlers()
  {
    foreach (var handler in _changeHandlers)
    {
      try
      {
        await handler();
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error occurred while invoking change handler.");
      }
    }
  }
}