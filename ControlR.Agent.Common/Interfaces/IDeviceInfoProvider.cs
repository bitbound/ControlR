namespace ControlR.Agent.Common.Interfaces;

public interface IDeviceInfoProvider
{
  Task<DeviceUpdateRequestDto> GetDeviceInfo();
}