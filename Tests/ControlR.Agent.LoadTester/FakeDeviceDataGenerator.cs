using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using ControlR.Agent.Common.Interfaces;
using ControlR.Agent.Common.Models;
using ControlR.Libraries.Shared.Dtos.HubDtos;
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
  : IDeviceInfoProvider
{
  private readonly Version _agentVersion = agentVersion;
  private readonly Guid _deviceId = DeterministicGuid.Create(deviceNumber);
  private readonly int _deviceNumber = deviceNumber;
  private readonly Guid _tenantId = tenantId;

  private double? _cpuUtilization;
  private string? _currentUser;
  private DeviceUpdateRequestDto? _device;
  private Drive? _osDrive;
  private double? _totalMemory;
  private double? _totalStorage;
  private double? _usedMemory;
  private double? _usedStorage;

  public Task<DeviceUpdateRequestDto> CreateDevice()
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

    _device ??= new DeviceUpdateRequestDto(
      Id: _deviceId,
      TenantId: _tenantId,
      Name: $"Test Device {_deviceNumber}",
      AgentVersion: $"{_agentVersion}",
      Is64Bit: Environment.Is64BitOperatingSystem,
      OsArchitecture: RuntimeInformation.OSArchitecture,
      OsDescription: RuntimeInformation.OSDescription,
      Platform: SystemEnvironment.Instance.Platform,
      ProcessorCount: Environment.ProcessorCount,
      CpuUtilization: _cpuUtilization.Value,
      TotalMemory: _totalMemory.Value,
      TotalStorage: _totalStorage.Value,
      UsedMemory: _usedMemory.Value,
      UsedStorage: _usedStorage.Value,
      CurrentUsers: [_currentUser],
      MacAddresses: [],
      LocalIpV4: "192.168.1.100",
      LocalIpV6: "fe80::1234:5678:9abc:def0",
      Drives: [_osDrive]
    );

    return _device.AsTaskResult();
  }
}