namespace ControlR.Libraries.Viewer.Common.State;

public interface IDeviceState : IStateBase
{
  DeviceDto CurrentDevice { get; set; }
  bool IsDeviceLoaded { get; }
  DeviceDto? TryGetCurrentDevice();
}

public class DeviceState(ILogger<DeviceState> logger) : StateBase(logger), IDeviceState
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

  public bool IsDeviceLoaded => _currentDevice != null;

  public DeviceDto? TryGetCurrentDevice() => _currentDevice;
}