namespace ControlR.Web.Client.Services.DeviceAccess;

public interface IDeviceAccessState
{
  DeviceDto CurrentDevice { get; set; }
  DeviceDto? CurrentDeviceMaybe { get; }
  bool IsDeviceLoaded { get; }
}

internal class DeviceAccessState : IDeviceAccessState
{
  private DeviceDto? _currentDevice;

  public DeviceDto CurrentDevice
  {
    get => _currentDevice ?? throw new InvalidOperationException("CurrentDevice is not set.");
    set => _currentDevice = value;
  }

  public DeviceDto? CurrentDeviceMaybe => _currentDevice;

  public bool IsDeviceLoaded => _currentDevice != null;
}