using ControlR.Libraries.Shared.Dtos;
using ControlR.Libraries.Shared.Models;

namespace ControlR.Agent.Interfaces;

public interface IDeviceDataGenerator
{
    Task<Device> CreateDevice(double cpuUtilization, IEnumerable<AuthorizedKeyDto> authorizedKeys, string deviceId);

    string GetAgentVersion();

    List<Drive> GetAllDrives();

    Device GetDeviceBase(IEnumerable<AuthorizedKeyDto> authorizedKeys, string deviceId);

    Task<(double usedGB, double totalGB)> GetMemoryInGB();

    (double usedStorage, double totalStorage) GetSystemDriveInfo();
}