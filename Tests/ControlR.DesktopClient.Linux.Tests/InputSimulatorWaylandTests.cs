using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System.Drawing;
using Microsoft.Win32.SafeHandles;
using ControlR.DesktopClient.Linux.Services;
using System.Runtime.CompilerServices;
using ControlR.DesktopClient.Linux.XdgPortal;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.DesktopClient.Common.Models;
using ControlR.DesktopClient.Common.Services;
using ControlR.Libraries.Shared.Primitives;
using ControlR.Libraries.Api.Contracts.Enums;
using ControlR.Libraries.Api.Contracts.Dtos.RemoteControlDtos;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ControlR.DesktopClient.Linux.Tests;

internal class FakePortal : IXdgDesktopPortal
{
  public List<(string session, int keycode, bool pressed)> KeyboardCalls { get; } = [];
  public string SessionHandle { get; set; } = "fake-session";

  public void Dispose() { }

  public Task<(SafeFileHandle Fd, string SessionHandle)?> GetPipeWireConnection() => Task.FromResult<(SafeFileHandle, string)?>((new SafeFileHandle(System.IntPtr.Zero, false), SessionHandle));

  public Task<string?> GetRemoteDesktopSessionHandle() => Task.FromResult<string?>(SessionHandle);

  public Task<List<PipeWireStreamInfo>> GetScreenCastStreams() => Task.FromResult(new List<PipeWireStreamInfo>());

  public Task Initialize(bool forceReinitialization = false, bool bypassRestoreToken = false)
  {
    return Task.CompletedTask;
  }

  public Task NotifyKeyboardKeycodeAsync(string sessionHandle, int keycode, bool pressed)
  {
    KeyboardCalls.Add((sessionHandle, keycode, pressed));
    return Task.CompletedTask;
  }

  public Task NotifyPointerAxisAsync(string sessionHandle, double dx, double dy, bool finish = true) => Task.CompletedTask;

  public Task NotifyPointerAxisDiscreteAsync(string sessionHandle, uint axis, int steps) => Task.CompletedTask;

  public Task NotifyPointerButtonAsync(string sessionHandle, int button, bool pressed) => Task.CompletedTask;

  public Task NotifyPointerMotionAbsoluteAsync(string sessionHandle, uint stream, double x, double y) => Task.CompletedTask;

  public Task NotifyPointerMotionAsync(string sessionHandle, double dx, double dy) => Task.CompletedTask;
}

internal class FakeDesktopCapturerFactory : IDesktopCapturerFactory
{
  public IDesktopCapturer CreateNew() => new FakeDesktopCapturer();
  public IDesktopCapturer GetOrCreate() => new FakeDesktopCapturer();

  private class FakeDesktopCapturer : IDesktopCapturer
  {
    public Task ChangeDisplays(string displayId) => Task.CompletedTask;
    public ValueTask DisposeAsync() => default;
    public string GetCaptureMode() => string.Empty;
    public async IAsyncEnumerable<DtoWrapper> GetCaptureStream([EnumeratorCancellation] System.Threading.CancellationToken cancellationToken) { yield break; }
    public double GetCurrentFps(System.TimeSpan window) => 0;
    public Task RequestKeyFrame() => Task.CompletedTask;
    public Task StartCapturingChanges(System.Threading.CancellationToken cancellationToken) => Task.CompletedTask;
    public Task<Result<ControlR.DesktopClient.Common.Models.DisplayInfo>> TryGetSelectedDisplay() => Task.FromResult(Result.Fail<DisplayInfo>("No display"));
  }
}

public class InputSimulatorWaylandTests
{
  [Fact]
  public async Task InvokeKeyEvent_RefreshesPortalSessionHandle_AfterPortalSessionChanges()
  {
    var portal = new FakePortal();
    var factory = new FakeDesktopCapturerFactory();
    var logger = NullLogger<InputSimulatorWayland>.Instance;

    var sim = new InputSimulatorWayland(portal, factory, logger);

    await sim.InvokeKeyEvent("Enter", string.Empty, true, KeyboardInputMode.Auto, KeyEventModifiersDto.None);
    portal.SessionHandle = "refreshed-session";
    await sim.InvokeKeyEvent("Enter", string.Empty, true, KeyboardInputMode.Auto, KeyEventModifiersDto.None);

    Assert.Equal(2, portal.KeyboardCalls.Count);
    Assert.Equal("fake-session", portal.KeyboardCalls[0].session);
    Assert.Equal("refreshed-session", portal.KeyboardCalls[1].session);
  }

  [Fact]
  public async Task InvokeKeyEvent_Uses_KeyName_When_Code_Is_Null()
  {
    var portal = new FakePortal();
    var factory = new FakeDesktopCapturerFactory();
    var logger = NullLogger<InputSimulatorWayland>.Instance;

    var sim = new InputSimulatorWayland(portal, factory, logger);

    await sim.InvokeKeyEvent("Enter", string.Empty, true, KeyboardInputMode.Auto, KeyEventModifiersDto.None);

    Assert.Single(portal.KeyboardCalls);
    Assert.Equal(28, portal.KeyboardCalls[0].keycode);
    Assert.True(portal.KeyboardCalls[0].pressed);
  }
}
