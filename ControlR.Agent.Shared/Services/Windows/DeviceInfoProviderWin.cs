using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using ControlR.Libraries.Shared.Services.FileSystem;
using ControlR.Libraries.NativeInterop.Windows;
using Windows.Win32;
using COMPUTER_NAME_FORMAT = Windows.Win32.System.SystemInformation.COMPUTER_NAME_FORMAT;

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

  protected override string GetDeviceName()
  {
    var physicalDnsHostName = TryGetWindowsComputerName(COMPUTER_NAME_FORMAT.ComputerNamePhysicalDnsHostname);
    if (!string.IsNullOrWhiteSpace(physicalDnsHostName))
    {
      return physicalDnsHostName;
    }

    return GetDnsHostName();
  }

  protected override string GetDnsHostName()
  {
    var dnsHostName = TryGetWindowsComputerName(COMPUTER_NAME_FORMAT.ComputerNameDnsHostname);
    if (!string.IsNullOrWhiteSpace(dnsHostName))
    {
      return dnsHostName;
    }

    return base.GetDnsHostName();
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

  private static string? TryGetWindowsComputerName(COMPUTER_NAME_FORMAT format)
  {
    try
    {
      uint size = 0;
      _ = PInvoke.GetComputerNameEx(format, null, ref size);
      if (size == 0)
      {
        return null;
      }

      var buffer = new char[size];

      if (!PInvoke.GetComputerNameEx(format, buffer.AsSpan(), ref size))
      {
        return null;
      }

      return new string(buffer, 0, (int)size).TrimEnd('\0');
    }
    catch
    {
      return null;
    }
  }
}