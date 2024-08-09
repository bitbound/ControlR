using ControlR.Agent.Interfaces;
using ControlR.Agent.Services.Base;
using Microsoft.Extensions.Logging;
using System.Runtime.Versioning;
using ControlR.Devices.Native.Services;
using ControlR.Libraries.Shared.Services;
using ControlR.Libraries.Shared.Models;
using ControlR.Libraries.DevicesNative.Windows;
using ControlR.Libraries.Shared.Dtos;

namespace ControlR.Agent.Services.Windows;

[SupportedOSPlatform("windows6.0.6000")]
internal class DeviceDataGeneratorWin(
    IWin32Interop _win32Interop,
    IEnvironmentHelper environmentHelper,
    ILogger<DeviceDataGeneratorWin> logger) : DeviceDataGeneratorBase(environmentHelper, logger), IDeviceDataGenerator
{
    private readonly ILogger<DeviceDataGeneratorWin> _logger = logger;

    public async Task<DeviceDto> CreateDevice(double cpuUtilization, IEnumerable<AuthorizedKeyDto> authorizedKeys, string deviceId)
    {
        try
        {
            var (usedStorage, totalStorage) = GetSystemDriveInfo();
            var (usedMemory, totalMemory) = await GetMemoryInGB();

            var currentUser = _win32Interop.GetActiveSessions().LastOrDefault()?.Username ?? string.Empty;
            var drives = GetAllDrives();
            var agentVersion = GetAgentVersion();

            return GetDeviceBase(
                authorizedKeys,
                deviceId,
                currentUser,
                drives,
                usedStorage,
                totalStorage,
                usedMemory,
                totalMemory,
                cpuUtilization,
                agentVersion);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting device info.");
            throw;
        }
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