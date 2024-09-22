namespace ControlR.Agent.Interfaces;

public interface IDeviceDataGenerator
{
  Task<DeviceDto> CreateDevice(double cpuUtilization, string deviceId);

  string GetAgentVersion();

  List<Drive> GetAllDrives();

  DeviceDto GetDeviceBase(
    string deviceId,
    string currentUser,
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