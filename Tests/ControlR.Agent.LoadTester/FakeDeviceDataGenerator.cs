﻿using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using ControlR.Libraries.Agent.Interfaces;
using ControlR.Libraries.Agent.Services.Base;
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
  private DeviceRequestDto? _device;

  public Task<DeviceRequestDto> CreateDevice(double cpuUtilization, Guid deviceId)
  {
    _device ??= new DeviceRequestDto
    {
      Name = $"Test Device {_deviceNumber}",
      AgentVersion = _agentVersion,
      TenantId = _tenantId,
      IsOnline = true,
      Platform = SystemEnvironment.Instance.Platform,
      ProcessorCount = Environment.ProcessorCount,
      OsArchitecture = RuntimeInformation.OSArchitecture,
      OsDescription = RuntimeInformation.OSDescription,
      Is64Bit = Environment.Is64BitOperatingSystem
    };
    return _device.AsTaskResult();
  }

  public new string GetAgentVersion()
  {
    return _agentVersion;
  }

  public Task<(double usedGB, double totalGB)> GetMemoryInGb()
  {
    return (0d, 0d).AsTaskResult();
  }
}