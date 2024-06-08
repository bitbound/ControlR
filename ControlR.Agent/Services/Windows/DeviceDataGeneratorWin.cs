using ControlR.Agent.Interfaces;
using ControlR.Agent.Services.Base;
using Microsoft.Extensions.Logging;
using System.Runtime.Versioning;
using ControlR.Devices.Native.Services;
using ControlR.Libraries.Shared.Services;
using ControlR.Libraries.Shared.Models;
using ControlR.Libraries.DevicesNative.Windows;

namespace ControlR.Agent.Services.Windows;

[SupportedOSPlatform("windows6.0.6000")]
internal class DeviceDataGeneratorWin(
    IWin32Interop _win32Interop,
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

            device.CurrentUser = _win32Interop.GetActiveSessions().LastOrDefault()?.Username ?? string.Empty;
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

            if (_win32Interop.GlobalMemoryStatus(ref memoryStatus))
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