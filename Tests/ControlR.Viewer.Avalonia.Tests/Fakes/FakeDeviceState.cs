using System.Runtime.InteropServices;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi;
using ControlR.Libraries.Api.Contracts.Enums;
using ControlR.Libraries.Shared.Collections;
using ControlR.Libraries.Viewer.Common.State;

namespace ControlR.Viewer.Avalonia.Tests.Fakes;

public sealed class FakeDeviceState : IDeviceState
{
  private readonly ConcurrentList<Func<Task>> _handlers = [];

  private DeviceResponseDto _currentDevice = CreateDefaultDevice();

  public DeviceResponseDto CurrentDevice
  {
    get => _currentDevice;
    set => _currentDevice = value;
  }
  public bool IsDeviceLoaded => _currentDevice.Id != Guid.Empty;

  public static DeviceResponseDto CreateDefaultDevice()
  {
    return new DeviceResponseDto(
      Name: "Test Device",
      AgentVersion: "1.0.0",
      CpuUtilization: 50.0,
      Id: Guid.NewGuid(),
      Is64Bit: true,
      IsOnline: true,
      LastSeen: DateTimeOffset.UtcNow,
      OsArchitecture: Architecture.X64,
      Platform: SystemPlatform.Windows,
      ProcessorCount: 8,
      ConnectionId: "test-connection",
      OsDescription: "Windows 11",
      TenantId: Guid.NewGuid(),
      TotalMemory: 16_000_000_000,
      TotalStorage: 500_000_000_000,
      UsedMemory: 8_000_000_000,
      UsedStorage: 250_000_000_000,
      CurrentUsers: ["testuser"],
      MacAddresses: [],
      PublicIpV4: "1.2.3.4",
      PublicIpV6: "::1",
      LocalIpV4: "192.168.1.1",
      LocalIpV6: "fe80::1",
      Drives: [],
      IsOutdated: false);
  }

  public Task NotifyStateChanged()
  {
    return InvokeHandlers();
  }

  public IDisposable OnStateChanged(Func<Task> callback)
  {
    _handlers.Add(callback);
    return new CallbackDisposable(() => _handlers.Remove(callback));
  }

  public DeviceResponseDto? TryGetCurrentDevice() => _currentDevice;

  private async Task InvokeHandlers()
  {
    foreach (var handler in _handlers)
    {
      await handler();
    }
  }

  private sealed class CallbackDisposable(Action dispose) : IDisposable
  {
    public void Dispose() => dispose();
  }
}
