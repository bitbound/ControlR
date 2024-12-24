using ControlR.Agent.Common.Models;

namespace ControlR.Agent.Common.Interfaces;

public interface IDeviceDataGenerator
{
  Task<DeviceModel> CreateDevice(Guid deviceId);

  string GetAgentVersion();

  IReadOnlyList<Drive> GetAllDrives();

  DeviceModel GetDeviceBase(
    Guid deviceId,
    string[] currentUsers,
    IReadOnlyList<Drive> drives,
    double usedStorage,
    double totalStorage,
    double usedMemory,
    double totalMemory,
    string agentVersion);

  Task<(double usedGB, double totalGB)> GetMemoryInGb();

  (double usedStorage, double totalStorage) GetSystemDriveInfo();
}