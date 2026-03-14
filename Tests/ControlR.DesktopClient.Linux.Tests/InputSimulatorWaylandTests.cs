using ControlR.DesktopClient.Linux.Services;
using ControlR.Libraries.Api.Contracts.Enums;
using ControlR.Libraries.Api.Contracts.Dtos.RemoteControlDtos;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ControlR.DesktopClient.Linux.Tests;

public class InputSimulatorWaylandTests
{
  [Fact]
  public async Task InvokeKeyEvent_Preserves_Auto_Printable_Text_Lifecycle()
  {
    var portal = new FakeXdgDesktopPortal();
    var factory = new FakeDesktopCapturerFactory();
    var logger = NullLogger<InputSimulatorWayland>.Instance;

    var sim = new InputSimulatorWayland(portal, factory, logger);

    await sim.InvokeKeyEvent("z", "KeyZ", true, KeyboardInputMode.Auto, KeyEventModifiersDto.None);
    await sim.InvokeKeyEvent("z", "KeyZ", false, KeyboardInputMode.Auto, KeyEventModifiersDto.None);

    var pressedStates = portal.KeysymCalls.Select(x => x.pressed)
      .Concat(portal.KeyboardCalls.Select(x => x.pressed))
      .ToList();

    Assert.Equal([true, false], pressedStates);
  }

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

    var calls = portal.KeysymCalls.Select(x => x.session)
      .Concat(portal.KeyboardCalls.Select(x => x.session))
      .ToList();

    Assert.Equal(2, calls.Count);
    Assert.Equal("fake-session", calls[0]);
    Assert.Equal("refreshed-session", calls[1]);
  }

  [Fact]
  public async Task InvokeKeyEvent_Uses_KeyName_When_Code_Is_Null()
  {
    var portal = new FakeXdgDesktopPortal();
    var factory = new FakeDesktopCapturerFactory();
    var logger = NullLogger<InputSimulatorWayland>.Instance;

    var sim = new InputSimulatorWayland(portal, factory, logger);

    await sim.InvokeKeyEvent("Enter", string.Empty, true, KeyboardInputMode.Auto, KeyEventModifiersDto.None);

    Assert.Single(portal.KeysymCalls.Concat(portal.KeyboardCalls.Select(x => (x.session, x.keycode, x.pressed))));
    Assert.True(
      // Keysym path: expect the Enter/Return keysym (0xff0d == 65293),
      // or fallback to the keyboard path with the Enter keycode (28).
      portal.KeysymCalls.Any(x => x.pressed && x.Item2 == 65293) ||
      portal.KeyboardCalls.Any(x => x.pressed && x.keycode == 28));
  }

  [Fact]
  public async Task ResetKeyboardState_Releases_Tracked_Key_Presses()
  {
    var portal = new FakeXdgDesktopPortal();
    var factory = new FakeDesktopCapturerFactory();
    var logger = NullLogger<InputSimulatorWayland>.Instance;

    var sim = new InputSimulatorWayland(portal, factory, logger);

    await sim.InvokeKeyEvent("z", "KeyZ", true, KeyboardInputMode.Auto, KeyEventModifiersDto.None);
    await sim.ResetKeyboardState();

    var pressedStates = portal.KeysymCalls.Select(x => x.pressed)
      .Concat(portal.KeyboardCalls.Select(x => x.pressed))
      .ToList();

    Assert.Equal([true, false], pressedStates);
  }
}
