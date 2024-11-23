using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using ControlR.Agent.Common.Interfaces;
using ControlR.Agent.Common.Models;
using ControlR.Agent.Common.Services.Base;
using ControlR.Libraries.Shared.Dtos.ServerApi;
using ControlR.Libraries.Shared.Extensions;
using ControlR.Libraries.Shared.Models;
using ControlR.Libraries.Shared.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ControlR.Agent.LoadTester;

[SupportedOSPlatform("windows6.0.6000")]
internal class FakeDeviceDataGenerator(
  int deviceNumber,
  Guid tenantId,
  ISystemEnvironment systemEnvironment,
  IOptionsMonitor<AgentAppOptions> appOptions,
  ILogger<FakeDeviceDataGenerator> logger)
  : DeviceDataGeneratorBase(systemEnvironment, appOptions, logger), IDeviceDataGenerator
{
  private readonly string _agentVersion = "0.9.15.0";
  private readonly int _deviceNumber = deviceNumber;
  private readonly Guid _tenantId = tenantId;
  private DeviceModel? _device;

  public Task<DeviceModel> CreateDevice(double cpuUtilization, Guid deviceId)
  {
    _device ??= new DeviceModel
    {
      Id = deviceId,
      Name = $"Test Device {_deviceNumber.ToString().PadLeft(2, '0')}",
      AgentVersion = _agentVersion,
      TenantId = _tenantId,
      IsOnline = true,
      Platform = SystemEnvironment.Instance.Platform,
      ProcessorCount = Environment.ProcessorCount,
      OsArchitecture = RuntimeInformation.OSArchitecture,
      OsDescription = RuntimeInformation.OSDescription,
      Is64Bit = Environment.Is64BitOperatingSystem,
      TotalMemory = 32,
      UsedMemory = 16,
      TotalStorage = 128,
      UsedStorage = 120,
      CpuUtilization = .1
    };
    return _device.AsTaskResult();
  }

  public new string GetAgentVersion()
  {
    return _agentVersion;
  }

  public Task<(double usedGB, double totalGB)> GetMemoryInGb()
  {
    return (50d, 128d).AsTaskResult();
  }
}