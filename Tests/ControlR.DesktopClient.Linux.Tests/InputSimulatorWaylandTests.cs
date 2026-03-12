using ControlR.DesktopClient.Linux.Services;
using ControlR.Libraries.Api.Contracts.Enums;
using ControlR.Libraries.Api.Contracts.Dtos.RemoteControlDtos;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ControlR.DesktopClient.Linux.Tests;

public class InputSimulatorWaylandTests
{
  [Fact]
  public async Task InvokeKeyEvent_RefreshesPortalSessionHandle_AfterPortalSessionChanges()
  {
    var portal = new FakeXdgDesktopPortal();
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
    var portal = new FakeXdgDesktopPortal();
    var factory = new FakeDesktopCapturerFactory();
    var logger = NullLogger<InputSimulatorWayland>.Instance;

    var sim = new InputSimulatorWayland(portal, factory, logger);

    await sim.InvokeKeyEvent("Enter", string.Empty, true, KeyboardInputMode.Auto, KeyEventModifiersDto.None);

    Assert.Single(portal.KeyboardCalls);
    Assert.Equal(28, portal.KeyboardCalls[0].keycode);
    Assert.True(portal.KeyboardCalls[0].pressed);
  }
}
