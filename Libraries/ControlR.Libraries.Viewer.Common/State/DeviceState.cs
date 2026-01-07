namespace ControlR.Libraries.Viewer.Common.State;

public interface IDeviceState : IStateBase
{
  DeviceResponseDto CurrentDevice { get; set; }
  bool IsDeviceLoaded { get; }
  DeviceResponseDto? TryGetCurrentDevice();
}

public class DeviceState(ILogger<DeviceState> logger) : StateBase(logger), IDeviceState
{
  private DeviceResponseDto? _currentDevice;

  public DeviceResponseDto CurrentDevice
  {
    get => _currentDevice ?? throw new InvalidOperationException("CurrentDevice is not set.");
    set
    {
      _currentDevice = value;
      NotifyStateChanged();
    }
  }

  public bool IsDeviceLoaded => _currentDevice != null;

  public DeviceResponseDto? TryGetCurrentDevice() => _currentDevice;
}