using ControlR.Agent.Common.Services.Linux;
using ControlR.Libraries.Shared.Primitives;
using ControlR.Libraries.Shared.Services.FileSystem;
using ControlR.Libraries.Shared.Services.Processes;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ControlR.Agent.Common.Tests;

/// <summary>
/// Pins the #154 rule: a display server counts as a login screen only when logind reports a
/// graphical greeter session for it. A container's virtual X server has no such session.
/// </summary>
public class DesktopEnvironmentDetectorAgentTests
{
  private readonly Mock<IFileSystem> _fileSystem = new();
  private readonly Mock<IProcessManager> _processManager = new();

  [Fact]
  public async Task DetectDisplayEnvironment_WhenActiveUserSessionExists_ReturnsNotLoginScreen()
  {
    SetupSessions("2");
    SetupSession("2", Session("1000", "x11", "user"));
    var sut = CreateSut();

    var result = await sut.DetectDisplayEnvironment();

    Assert.False(result.IsLoginScreen);
    _processManager.Verify(
      x => x.GetProcessOutput("systemctl", It.IsAny<string>(), It.IsAny<int>()),
      Times.Never);
  }

  [Fact]
  public async Task DetectDisplayEnvironment_WhenNoGreeterSession_ReturnsNotLoginScreen()
  {
    SetupSessions();
    var sut = CreateSut();

    var result = await sut.DetectDisplayEnvironment();

    Assert.False(result.IsLoginScreen);
    Assert.False(result.IsWayland);
    _processManager.Verify(
      x => x.GetProcessOutput("systemctl", It.IsAny<string>(), It.IsAny<int>()),
      Times.Never);
  }

  [Fact]
  public async Task DetectDisplayEnvironment_WhenWaylandGreeter_DoesNotRequireXAuth()
  {
    SetupSessions("2");
    SetupSession("2", Session("120", "wayland", "greeter"));
    _fileSystem.Setup(x => x.FileExists("/run/user/120/wayland-0")).Returns(true);
    var sut = CreateSut();

    var result = await sut.DetectDisplayEnvironment();

    Assert.True(result.IsLoginScreen);
    Assert.True(result.IsWayland);
    Assert.Equal("/run/user/120", result.WaylandRuntimeDir);
    Assert.Null(result.XAuthPath);
    _processManager.Verify(
      x => x.GetProcessOutput("systemctl", It.IsAny<string>(), It.IsAny<int>()),
      Times.Never);
  }

  [Fact]
  public async Task DetectDisplayEnvironment_WhenX11GreeterHasNoXAuth_ReturnsNullXAuth()
  {
    SetupSessions("2");
    SetupSession("2", Session("120", "x11", "greeter"));
    _processManager
      .Setup(x => x.GetProcessOutput("systemctl", It.IsAny<string>(), It.IsAny<int>()))
      .ReturnsAsync(Result.Fail<string>("unit not found"));
    _processManager
      .Setup(x => x.GetProcessOutput("ps", "aux", It.IsAny<int>()))
      .ReturnsAsync(Result.Ok(""));
    _fileSystem.Setup(x => x.FileExists(It.IsAny<string>())).Returns(false);
    var sut = CreateSut();

    var result = await sut.DetectDisplayEnvironment();

    Assert.True(result.IsLoginScreen);
    Assert.Null(result.XAuthPath);
  }

  [Fact]
  public async Task DetectDisplayEnvironment_WhenX11Greeter_ReturnsDetectedXAuth()
  {
    SetupSessions("2");
    SetupSession("2", Session("120", "x11", "greeter"));
    _processManager
      .Setup(x => x.GetProcessOutput("systemctl", "status display-manager --no-pager -l", It.IsAny<int>()))
      .ReturnsAsync(Result.Ok("lightdm.service"));
    _fileSystem.Setup(x => x.FileExists("/run/lightdm/root/:0")).Returns(true);
    _fileSystem.Setup(x => x.FileExists("/tmp/.X11-unix/X0")).Returns(true);
    var sut = CreateSut();

    var result = await sut.DetectDisplayEnvironment();

    Assert.True(result.IsLoginScreen);
    Assert.False(result.IsWayland);
    Assert.Equal("lightdm", result.DisplayManager);
    Assert.Equal("/run/lightdm/root/:0", result.XAuthPath);
    Assert.Equal(":0", result.Display);
  }

  private static string Session(string user, string sessionType, string sessionClass)
  {
    return string.Join('\n',
      "Id=2",
      $"Class={sessionClass}",
      $"Type={sessionType}",
      $"User={user}",
      "State=active",
      "Active=yes");
  }

  private DesktopEnvironmentDetectorAgent CreateSut()
  {
    return new DesktopEnvironmentDetectorAgent(
      _processManager.Object,
      _fileSystem.Object,
      NullLogger<DesktopEnvironmentDetectorAgent>.Instance);
  }

  private void SetupSession(string sessionId, string sessionOutput)
  {
    _processManager
      .Setup(x => x.GetProcessOutput("loginctl", $"show-session {sessionId}", It.IsAny<int>()))
      .ReturnsAsync(Result.Ok(sessionOutput));
  }

  private void SetupSessions(params string[] sessionIds)
  {
    _processManager
      .Setup(x => x.GetProcessOutput("loginctl", "list-sessions --no-legend", It.IsAny<int>()))
      .ReturnsAsync(Result.Ok(string.Join('\n', sessionIds)));
  }
}
