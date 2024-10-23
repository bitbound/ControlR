using System.Runtime.Versioning;
using ControlR.Devices.Native.Services;
using ControlR.Libraries.Agent.Interfaces;
using ControlR.Libraries.Agent.Services.Base;
using ControlR.Libraries.DevicesNative.Windows;
using ControlR.Libraries.Shared.Dtos.ServerApi;

namespace ControlR.Libraries.Agent.Services.Windows;

[SupportedOSPlatform("windows6.0.6000")]
internal class DeviceDataGeneratorWin(
  IWin32Interop win32Interop,
  ISystemEnvironment environmentHelper,
  ILogger<DeviceDataGeneratorWin> logger) : DeviceDataGeneratorBase(environmentHelper, logger), IDeviceDataGenerator
{
  private readonly ILogger<DeviceDataGeneratorWin> _logger = logger;

  public virtual async Task<DeviceRequestDto> CreateDevice(double cpuUtilization, Guid deviceId)
  {
    try
    {
      var (usedStorage, totalStorage) = GetSystemDriveInfo();
      var (usedMemory, totalMemory) = await GetMemoryInGb();

      var currentUsers = win32Interop.GetActiveSessions()
        .Select(x => x.Username)
        .Where(x => !string.IsNullOrWhiteSpace(x))
        .ToArray();

      var drives = GetAllDrives();
      var agentVersion = GetAgentVersion();

      return GetDeviceBase(
        deviceId,
        currentUsers,
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

  public Task<(double usedGB, double totalGB)> GetMemoryInGb()
  {
    double totalGb = 0;
    double freeGb = 0;

    try
    {
      var memoryStatus = new MemoryStatusEx();

      if (win32Interop.GlobalMemoryStatus(ref memoryStatus))
      {
        freeGb = Math.Round((double)memoryStatus.ullAvailPhys / 1024 / 1024 / 1024, 2);
        totalGb = Math.Round((double)memoryStatus.ullTotalPhys / 1024 / 1024 / 1024, 2);
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while getting device memory.");
    }

    return Task.FromResult((totalGb - freeGb, totalGB: totalGb));
  }
}