namespace ControlR.Web.Client.Services.DeviceAccess;

public interface IDeviceState : IStateBase
{
  DeviceDto CurrentDevice { get; set; }
  DeviceDto? CurrentDeviceMaybe { get; }
  bool IsDeviceLoaded { get; }
}

internal class DeviceState(ILogger<DeviceState> logger) : StateBase(logger), IDeviceState
{
  private DeviceDto? _currentDevice;

  public DeviceDto CurrentDevice
  {
    get => _currentDevice ?? throw new InvalidOperationException("CurrentDevice is not set.");
    set
    {
      _currentDevice = value;
      NotifyStateChanged();
    }
  }

  public DeviceDto? CurrentDeviceMaybe => _currentDevice;

  public bool IsDeviceLoaded => _currentDevice != null;
}