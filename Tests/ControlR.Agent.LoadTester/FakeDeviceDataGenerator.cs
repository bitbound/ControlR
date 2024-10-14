using ControlR.Agent.Interfaces;
using ControlR.Agent.Services.Windows;
using ControlR.Devices.Native.Services;
using ControlR.Libraries.Shared.Dtos;
using ControlR.Libraries.Shared.Services;
using Microsoft.Extensions.Logging;
using System.Runtime.Versioning;

namespace ControlR.Agent.LoadTester;

[SupportedOSPlatform("windows6.0.6000")]
internal class FakeDeviceDataGenerator : DeviceDataGeneratorWin, IDeviceDataGenerator
{
  private readonly int _deviceNumber;

  public FakeDeviceDataGenerator(
    int deviceNumber,
    IWin32Interop win32Interop, 
    ISystemEnvironment environmentHelper, 
    ILogger<DeviceDataGeneratorWin> logger) 
    : base(win32Interop, environmentHelper, logger)
  {
    _deviceNumber = deviceNumber;
  }

  
  public override async Task<DeviceFromAgentDto> CreateDevice(double cpuUtilization, Guid deviceId)
  {
    var device = await  base.CreateDevice(cpuUtilization, deviceId);
    device.Name = $"Test Device {_deviceNumber}";
    device.AgentVersion = "0.9.15.0";
    return device;
  }
}
