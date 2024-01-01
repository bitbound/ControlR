using ControlR.Devices.Common.Native.Windows;
using ControlR.Devices.Common.Services.Base;
using ControlR.Devices.Common.Services.Interfaces;
using ControlR.Shared.Models;
using ControlR.Shared.Services;
using Microsoft.Extensions.Logging;
using System.Runtime.Versioning;

namespace ControlR.Devices.Common.Services.Windows;

[SupportedOSPlatform("windows6.0.6000")]
internal class DeviceDataGeneratorWin(
    IEnvironmentHelper environmentHelper,
    ILogger<DeviceDataGeneratorWin> logger) : DeviceDataGeneratorBase(environmentHelper, logger), IDeviceDataGenerator
{
    private readonly ILogger<DeviceDataGeneratorWin> _logger = logger;

    public async Task<Device> CreateDevice(double cpuUtilization, IEnumerable<string> authorizedKeys, string deviceId)
    {
        var device = GetDeviceBase(authorizedKeys, deviceId);

        try
        {
            var (usedStorage, totalStorage) = GetSystemDriveInfo();
            var (usedMemory, totalMemory) = await GetMemoryInGB();

            device.CurrentUser = Win32.GetActiveSessions().LastOrDefault()?.Username ?? string.Empty;
            device.Drives = GetAllDrives();
            device.UsedStorage = usedStorage;
            device.TotalStorage = totalStorage;
            device.UsedMemory = usedMemory;
            device.TotalMemory = totalMemory;
            device.CpuUtilization = cpuUtilization;
            device.AgentVersion = GetAgentVersion();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting device info.");
        }

        return device;
    }

    public Task<(double usedGB, double totalGB)> GetMemoryInGB()
    {
        double totalGB = 0;
        double freeGB = 0;

        try
        {
            var memoryStatus = new MemoryStatusEx();

            if (Win32.GlobalMemoryStatusEx(ref memoryStatus))
            {
                freeGB = Math.Round((double)memoryStatus.ullAvailPhys / 1024 / 1024 / 1024, 2);
                totalGB = Math.Round((double)memoryStatus.ullTotalPhys / 1024 / 1024 / 1024, 2);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while getting device memory.");
        }

        return Task.FromResult((totalGB - freeGB, totalGB));
    }
}