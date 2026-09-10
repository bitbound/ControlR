using ControlR.Libraries.Shared.Services.StateManagement;

namespace ControlR.Libraries.Viewer.Common.State;

public interface IDeviceAccessPermissionsState : IStateBase
{
  DeviceAccessPermissionsDto? Permissions { get; set; }

  void Clear();
}

public class DeviceAccessPermissionsState(ILogger<DeviceAccessPermissionsState> logger) : ObservableState(logger), IDeviceAccessPermissionsState
{
  public DeviceAccessPermissionsDto? Permissions
  {
    get => Get<DeviceAccessPermissionsDto?>();
    set => Set(value);
  }

  public void Clear()
  {
    Permissions = null;
  }
}
