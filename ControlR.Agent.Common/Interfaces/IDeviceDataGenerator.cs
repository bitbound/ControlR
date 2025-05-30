using ControlR.Agent.Common.Models;

namespace ControlR.Agent.Common.Interfaces;

public interface IDeviceDataGenerator
{
  Task<DeviceModel> CreateDevice(Guid deviceId);
}