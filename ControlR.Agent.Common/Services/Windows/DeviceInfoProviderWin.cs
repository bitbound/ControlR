using System.Runtime.Versioning;
using ControlR.Agent.Common.Interfaces;
using ControlR.Agent.Common.Services.Base;
using ControlR.Libraries.NativeInterop.Windows;

namespace ControlR.Agent.Common.Services.Windows;

[SupportedOSPlatform("windows6.0.6000")]
internal class DeviceInfoProviderWin(
  IWin32Interop win32Interop,
  ISystemEnvironment environmentHelper,
  ICpuUtilizationSampler cpuUtilizationSampler,
  ISettingsProvider settingsProvider,
  ILogger<DeviceInfoProviderWin> logger)
  : DeviceInfoProviderBase(environmentHelper, cpuUtilizationSampler, settingsProvider, logger), IDeviceInfoProvider
{
  private readonly ILogger<DeviceInfoProviderWin> _logger = logger;

  public async Task<DeviceUpdateRequestDto> CreateDevice()
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

      return CreateDeviceBase(
        currentUsers,
        drives,
        usedStorage,
        totalStorage,
        usedMemory,
        totalMemory,
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