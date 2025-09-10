using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using ControlR.Agent.Common.Models;
using Microsoft.Extensions.Options;

namespace ControlR.Agent.Common.Services.Base;

internal class DeviceDataGeneratorBase(
  ISystemEnvironment environmentHelper,
  ICpuUtilizationSampler cpuSampler,
  IOptionsMonitor<AgentAppOptions> appOptions,
  ILogger<DeviceDataGeneratorBase> logger)
{
  private readonly IOptionsMonitor<AgentAppOptions> _appOptions = appOptions;
  private readonly ICpuUtilizationSampler _cpuSampler = cpuSampler;
  private readonly ISystemEnvironment _environmentHelper = environmentHelper;
  private readonly ILogger<DeviceDataGeneratorBase> _logger = logger;

  protected string GetAgentVersion()
  {
    var version = typeof(DeviceDataGeneratorBase).Assembly.GetName().Version?.ToString();
    if (!string.IsNullOrWhiteSpace(version))
    {
      return version;
    }

    _logger.LogWarning("Failed to obtain agent version.");
    return "0.0.0.0";
  }

  protected IReadOnlyList<Drive> GetAllDrives()
  {
    try
    {
      return
      [
        .. DriveInfo.GetDrives()
          .Where(x => x.IsReady)
          .Where(x => x.DriveType == DriveType.Fixed)
          .Where(x => x.DriveFormat is not "squashfs" and not "overlay")
          .Where(x => !x.RootDirectory.FullName.StartsWith("/System/Volumes/"))
          .Where(x => x.TotalSize > 0)
          .Select(x => new Drive
          {
            DriveFormat = x.DriveFormat,
            DriveType = x.DriveType,
            Name = x.Name,
            RootDirectory = x.RootDirectory.FullName,
            FreeSpace = x.TotalFreeSpace > 0
              ? Math.Round((double)x.TotalFreeSpace / 1024 / 1024 / 1024, 2)
              : 0,
            TotalSize = x.TotalSize > 0
              ? Math.Round((double)x.TotalSize / 1024 / 1024 / 1024, 2)
              : 0,
            VolumeLabel = x.VolumeLabel
          })
      ];
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error getting drive info.");
      return [];
    }
  }

  protected DeviceModel GetDeviceBase(
    Guid deviceId,
    string[] currentUsers,
    IReadOnlyList<Drive> drives,
    double usedStorage,
    double totalStorage,
    double usedMemory,
    double totalMemory,
    string agentVersion)
  {
    return new DeviceModel
    {
      Id = deviceId,
      TenantId = _appOptions.CurrentValue.TenantId,
      CurrentUsers = currentUsers,
      CpuUtilization = _cpuSampler.CurrentUtilization,
      Drives = drives,
      AgentVersion = agentVersion,
      UsedStorage = usedStorage,
      TotalStorage = totalStorage,
      UsedMemory = usedMemory,
      TotalMemory = totalMemory,
      Name = Environment.MachineName,
      Platform = _environmentHelper.Platform,
      ProcessorCount = Environment.ProcessorCount,
      OsArchitecture = RuntimeInformation.OSArchitecture,
      OsDescription = RuntimeInformation.OSDescription,
      Is64Bit = Environment.Is64BitOperatingSystem,
      MacAddresses = [.. GetMacAddresses()],
      LocalIpV4 = GetLocalIpV4(),
      LocalIpV6 = GetLocalIpV6()
    };
  }

  protected (double usedStorage, double totalStorage) GetSystemDriveInfo()
  {
    try
    {
      DriveInfo? systemDrive;

      var allDrives = DriveInfo.GetDrives();

      if (_environmentHelper.IsWindows)
      {
        var rootDir = Path.GetPathRoot(Environment.SystemDirectory) ?? string.Empty;

        systemDrive = allDrives.FirstOrDefault(x =>
          x.IsReady &&
          x.RootDirectory.FullName.Contains(rootDir));
      }
      else
      {
        systemDrive = allDrives.FirstOrDefault(x =>
          x.IsReady &&
          x.RootDirectory.FullName == Path.GetPathRoot(Environment.CurrentDirectory));
      }

      if (systemDrive is { TotalSize: > 0, TotalFreeSpace: > 0 })
      {
        var totalStorage = Math.Round((double)systemDrive.TotalSize / 1024 / 1024 / 1024, 2);
        var usedStorage =
          Math.Round(((double)systemDrive.TotalSize - systemDrive.TotalFreeSpace) / 1024 / 1024 / 1024, 2);

        return (usedStorage, totalStorage);
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error getting system drive info.");
    }

    return (0, 0);
  }

  private string GetLocalIpV4()
  {
    try
    {
      var nics = NetworkInterface.GetAllNetworkInterfaces();

      var activeNic = nics
        .FirstOrDefault(c => c.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                             c.OperationalStatus == OperationalStatus.Up);

      if (activeNic != null)
      {
        var ipProperties = activeNic.GetIPProperties();
        var ipv4Address = ipProperties.UnicastAddresses
          .FirstOrDefault(addr => addr.Address.AddressFamily == AddressFamily.InterNetwork);

        if (ipv4Address != null)
        {
          return ipv4Address.Address.ToString();
        }
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while getting local IPv4 address.");
    }

    return string.Empty;
  }

  private string GetLocalIpV6()
  {
    try
    {
      var nics = NetworkInterface.GetAllNetworkInterfaces();

      var activeNic = nics
        .FirstOrDefault(c => c.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                             c.OperationalStatus == OperationalStatus.Up);

      if (activeNic != null)
      {
        var ipProperties = activeNic.GetIPProperties();
        var ipv6Address = ipProperties.UnicastAddresses
          .FirstOrDefault(addr =>
            addr.Address is { AddressFamily: AddressFamily.InterNetworkV6, IsIPv6LinkLocal: false, IsIPv6SiteLocal: false, IsIPv6Teredo: false });

        if (ipv6Address != null)
        {
          return ipv6Address.Address.ToString();
        }
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while getting local IPv6 address.");
    }

    return string.Empty;
  }

  private List<string> GetMacAddresses()
  {
    var macAddress = new List<string>();

    try
    {
      var nics = NetworkInterface.GetAllNetworkInterfaces();

      if (nics.Length == 0)
      {
        return macAddress;
      }

      var onlineNics = nics
        .Where(c =>
          c.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
          c.OperationalStatus == OperationalStatus.Up);

      foreach (var adapter in onlineNics)
      {
        var ipProperties = adapter.GetIPProperties();

        var unicastAddresses = ipProperties.UnicastAddresses;
        if (unicastAddresses.All(temp => temp.Address.AddressFamily != AddressFamily.InterNetwork))
        {
          continue;
        }

        var address = adapter.GetPhysicalAddress();
        macAddress.Add(address.ToString());
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while getting MAC addresses.");
    }

    return macAddress;
  }
}