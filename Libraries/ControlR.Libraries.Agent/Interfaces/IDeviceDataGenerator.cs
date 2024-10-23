using ControlR.Libraries.Shared.Dtos.ServerApi;

namespace ControlR.Libraries.Agent.Interfaces;

public interface IDeviceDataGenerator
{
  Task<DeviceRequestDto> CreateDevice(double cpuUtilization, Guid deviceId);

  string GetAgentVersion();

  List<Drive> GetAllDrives();

  DeviceRequestDto GetDeviceBase(
    Guid deviceId,
    string[] currentUsers,
    List<Drive> drives,
    double usedStorage,
    double totalStorage,
    double usedMemory,
    double totalMemory,
    double cpuUtilization,
    string agentVersion);

  Task<(double usedGB, double totalGB)> GetMemoryInGb();

  (double usedStorage, double totalStorage) GetSystemDriveInfo();
}