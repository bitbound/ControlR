using System.Runtime.Versioning;
using ControlR.Libraries.Shared.Services.FileSystem;
using ControlR.Libraries.NativeInterop.Windows;

namespace ControlR.Agent.Shared.Services.Windows;

[SupportedOSPlatform("windows8.0")]
public class DeviceInfoProviderWin(
  IWin32Interop win32Interop,
  IFileSystem fileSystem,
  ISystemEnvironment environmentHelper,
  ICpuUtilizationSampler cpuUtilizationSampler,
  IOptionsAccessor optionsAccessor,
  ILogger<DeviceInfoProviderWin> logger)
  : DeviceInfoProviderBase(fileSystem, environmentHelper, cpuUtilizationSampler, optionsAccessor, logger), IDeviceInfoProvider
{
  private readonly ILogger<DeviceInfoProviderWin> _logger = logger;

  protected override Task<string[]> GetCurrentUsers()
  {
    return win32Interop.GetActiveSessions()
        .Select(x => x.Username)
        .Where(x => !string.IsNullOrWhiteSpace(x))
        .ToArray()
        .AsTaskResult();
  }

  protected override Task<MemoryInfo> GetMemoryInGb()
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

    var memoryInfo = new MemoryInfo(totalGb - freeGb, totalGb);
    return Task.FromResult(memoryInfo);
  }
}