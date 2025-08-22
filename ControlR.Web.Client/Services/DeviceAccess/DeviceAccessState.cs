namespace ControlR.Web.Client.Services.DeviceAccess;

public interface IDeviceAccessState
{
  DeviceDto CurrentDevice { get; set; }
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

  public bool IsDeviceLoaded => _currentDevice != null;
}