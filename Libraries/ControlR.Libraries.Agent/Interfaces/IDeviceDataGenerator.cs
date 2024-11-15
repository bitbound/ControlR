using ControlR.Libraries.Agent.Models;

namespace ControlR.Libraries.Agent.Interfaces;

public interface IDeviceDataGenerator
{
  Task<DeviceModel> CreateDevice(double cpuUtilization, Guid deviceId);

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
    double cpuUtilization,
    string agentVersion);

  Task<(double usedGB, double totalGB)> GetMemoryInGb();

  (double usedStorage, double totalStorage) GetSystemDriveInfo();
}