using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using ControlR.Agent.Common.Interfaces;
using ControlR.Agent.Common.Models;
using ControlR.Libraries.Shared.Extensions;
using ControlR.Libraries.Shared.Helpers;
using ControlR.Libraries.Shared.Models;
using ControlR.Libraries.Shared.Services;

namespace ControlR.Agent.LoadTester;

[SupportedOSPlatform("windows6.0.6000")]
internal class FakeDeviceDataGenerator(
  int deviceNumber,
  Guid tenantId,
  Version agentVersion)
  : IDeviceDataGenerator
{
  private readonly Version _agentVersion = agentVersion;
  private readonly int _deviceNumber = deviceNumber;
  private readonly Guid _tenantId = tenantId;
  private DeviceModel? _device;
  private double? _totalMemory;
  private double? _usedMemory;
  private double? _totalStorage;
  private double? _usedStorage;
  private double? _cpuUtilization;
  private string? _currentUser;
  private Drive? _osDrive;

  public Task<DeviceModel> CreateDevice(Guid deviceId)
  {
    _totalMemory ??= Random.Shared.Next(4, 128);
    _usedMemory ??= Math.Clamp(_totalMemory.Value * Random.Shared.NextDouble(), 2, _totalMemory.Value - .25);
    _totalStorage ??= Random.Shared.Next(64, 4_000);
    _usedStorage ??= Math.Clamp(_totalStorage.Value * Random.Shared.NextDouble(), 30, _totalStorage.Value - .5);
    _cpuUtilization = Random.Shared.NextDouble();
    _currentUser ??= RandomGenerator.GenerateString(8);
    _osDrive = new Drive()
    {
      DriveFormat = "NTFS",
      DriveType = DriveType.Fixed,
      Name = "C:\\",
      TotalSize = _totalStorage.Value * 1_073_741_824, // Convert GB to bytes
      FreeSpace = _totalStorage.Value * 1_073_741_824 - _usedStorage.Value * 1_073_741_824, // Convert GB to bytes,
      RootDirectory = "C:\\",
      VolumeLabel = "OS",
    };

    _device ??= new DeviceModel
    {
      Id = deviceId,
      Name = $"Test Device {_deviceNumber}",
      AgentVersion = $"{_agentVersion}",
      TenantId = _tenantId,
      IsOnline = true,
      Platform = SystemEnvironment.Instance.Platform,
      ProcessorCount = Environment.ProcessorCount,
      OsArchitecture = RuntimeInformation.OSArchitecture,
      OsDescription = RuntimeInformation.OSDescription,
      Is64Bit = Environment.Is64BitOperatingSystem,
      TotalMemory = _totalMemory.Value,
      UsedMemory = _usedMemory.Value,
      TotalStorage = _totalStorage.Value,
      UsedStorage = _usedStorage.Value,
      CpuUtilization = _cpuUtilization.Value,
      CurrentUsers = [_currentUser],
      Drives = [_osDrive],
    };
    return _device.AsTaskResult();
  }
}