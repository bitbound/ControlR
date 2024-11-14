using System.Collections.Immutable;
using ControlR.Libraries.Shared.Dtos.ServerApi;
using DeviceUpdateRequestDto = ControlR.Libraries.Shared.Dtos.ServerApi.DeviceUpdateRequestDto;

namespace ControlR.Libraries.Agent.Interfaces;

public interface IDeviceDataGenerator
{
  Task<DeviceUpdateRequestDto> CreateDevice(double cpuUtilization, Guid deviceId);

  string GetAgentVersion();

  IReadOnlyList<Drive> GetAllDrives();

  DeviceUpdateRequestDto GetDeviceBase(
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