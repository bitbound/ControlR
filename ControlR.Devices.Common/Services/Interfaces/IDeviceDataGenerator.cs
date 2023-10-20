using ControlR.Shared.Models;

namespace ControlR.Devices.Common.Services.Interfaces;

public interface IDeviceDataGenerator
{
    Task<Device> CreateDevice(double cpuUtilization, IEnumerable<string> authorizedKeys, string deviceId);

    string GetAgentVersion();

    List<Drive> GetAllDrives();

    Device GetDeviceBase(IEnumerable<string> authorizedKeys, string deviceId);

    Task<(double usedGB, double totalGB)> GetMemoryInGB();

    (double usedStorage, double totalStorage) GetSystemDriveInfo();
}