using ControlR.Agent.Common.Services.Linux;
using ControlR.Libraries.Shared.Primitives;
using ControlR.Libraries.Shared.Services.FileSystem;
using ControlR.Libraries.Shared.Services.Processes;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ControlR.Agent.Common.Tests;

/// <summary>
/// Pins Linux login-screen detection: a display server counts as a login screen only when
/// logind reports a greeter session for it. See #154.
/// </summary>
public class DesktopEnvironmentDetectorAgentTests
{
  private readonly Mock<IFileSystem> _fileSystem = new();
  private readonly Mock<IProcessManager> _processManager = new();

  [Fact]
  public async Task DetectDisplayEnvironment_WhenActiveUserAndGreeterExist_ReturnsNotLoginScreen()
  {
    SetupSessions("2", "3");
    SetupSession("2", Session("120", "x11", "greeter"));
    SetupSession("3", Session("1000", "x11", "user"));
    var sut = CreateSut();

    var result = await sut.DetectDisplayEnvironment();

    Assert.False(result.IsLoginScreen);
    _processManager.Verify(
      x => x.GetProcessOutput("systemctl", It.IsAny<string>(), It.IsAny<int>()),
      Times.Never);
  }

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
  public async Task DetectDisplayEnvironment_WhenGreeterSessionHasNoType_FallsBackToX11()
  {
    SetupSessions("2");
    SetupSession("2", Session("120", null, "greeter"));
    _processManager
      .Setup(x => x.GetProcessOutput("systemctl", "status display-manager --no-pager -l", It.IsAny<int>()))
      .ReturnsAsync(Result.Ok("lightdm.service"));
    _fileSystem.Setup(x => x.FileExists("/run/lightdm/root/:0")).Returns(true);
    var sut = CreateSut();

    var result = await sut.DetectDisplayEnvironment();

    Assert.True(result.IsLoginScreen);
    Assert.False(result.IsWayland);
    Assert.Equal("lightdm", result.DisplayManager);
  }

  [Fact]
  public async Task DetectDisplayEnvironment_WhenGreeterSessionIsClosing_ReturnsNotLoginScreen()
  {
    SetupSessions("2");
    SetupSession("2", Session("120", "x11", "greeter", state: "closing"));
    var sut = CreateSut();

    var result = await sut.DetectDisplayEnvironment();

    Assert.True(result.IsConclusive);
    Assert.False(result.IsLoginScreen);
  }

  [Fact]
  public async Task DetectDisplayEnvironment_WhenLoginctlFails_ReturnsInconclusive()
  {
    SetupSessionsFailure();
    var sut = CreateSut();

    var result = await sut.DetectDisplayEnvironment();

    Assert.False(result.IsConclusive);
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

    Assert.True(result.IsConclusive);
    Assert.False(result.IsLoginScreen);
    Assert.False(result.IsWayland);
    _processManager.Verify(
      x => x.GetProcessOutput("systemctl", It.IsAny<string>(), It.IsAny<int>()),
      Times.Never);
  }

  [Fact]
  public async Task DetectDisplayEnvironment_WhenSessionDetailFails_ReturnsInconclusive()
  {
    SetupSessions("2");
    _processManager
      .Setup(x => x.GetProcessOutput("loginctl", "show-session 2", It.IsAny<int>()))
      .ReturnsAsync(Result.Ok(Session("120", "x11", "greeter")));
    _processManager
      .Setup(x => x.GetProcessOutputStrict("loginctl", "show-session 2", It.IsAny<int>()))
      .ReturnsAsync(Result.Fail<string>("exited with code 1"));
    var sut = CreateSut();

    var result = await sut.DetectDisplayEnvironment();

    Assert.False(result.IsConclusive);
    Assert.False(result.IsLoginScreen);
  }

  [Fact]
  public async Task DetectDisplayEnvironment_WhenSessionIsNotGreeter_ReturnsNotLoginScreen()
  {
    SetupSessions("2");
    SetupSession("2", Session("120", "x11", "user"));
    var sut = CreateSut();

    var result = await sut.DetectDisplayEnvironment();

    Assert.True(result.IsConclusive);
    Assert.False(result.IsLoginScreen);
    _processManager.Verify(
      x => x.GetProcessOutput("systemctl", It.IsAny<string>(), It.IsAny<int>()),
      Times.Never);
  }

  [Fact]
  public async Task DetectDisplayEnvironment_WhenWaylandGreeterHasNoSocket_FallsBackToX11()
  {
    SetupSessions("2");
    SetupSession("2", Session("120", "wayland", "greeter"));
    _processManager
      .Setup(x => x.GetProcessOutput("systemctl", "status display-manager --no-pager -l", It.IsAny<int>()))
      .ReturnsAsync(Result.Ok("lightdm.service"));
    _fileSystem.Setup(x => x.FileExists("/run/lightdm/root/:0")).Returns(true);
    var sut = CreateSut();

    var result = await sut.DetectDisplayEnvironment();

    Assert.True(result.IsLoginScreen);
    Assert.False(result.IsWayland);
    Assert.Equal("lightdm", result.DisplayManager);
    Assert.Equal("/run/lightdm/root/:0", result.XAuthPath);
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
  public async Task DetectDisplayEnvironment_WhenX11Greeter_ReturnsDetectedDisplayAndXAuth()
  {
    SetupSessions("2");
    SetupSession("2", Session("120", "x11", "greeter"));
    _processManager
      .Setup(x => x.GetProcessOutput("systemctl", "status display-manager --no-pager -l", It.IsAny<int>()))
      .ReturnsAsync(Result.Ok("lightdm.service"));
    _fileSystem.Setup(x => x.FileExists("/run/lightdm/root/:0")).Returns(true);
    _fileSystem.Setup(x => x.FileExists("/tmp/.X11-unix/X0")).Returns(false);
    _fileSystem.Setup(x => x.FileExists("/tmp/.X11-unix/X1")).Returns(true);
    var sut = CreateSut();

    var result = await sut.DetectDisplayEnvironment();

    Assert.True(result.IsLoginScreen);
    Assert.False(result.IsWayland);
    Assert.Equal("lightdm", result.DisplayManager);
    Assert.Equal("/run/lightdm/root/:0", result.XAuthPath);
    Assert.Equal(":1", result.Display);
  }

  private static string Session(
    string user,
    string? sessionType,
    string sessionClass,
    string state = "active")
  {
    var lines = new List<string>
    {
      "Id=2",
      $"Class={sessionClass}"
    };

    if (sessionType is not null)
    {
      lines.Add($"Type={sessionType}");
    }

    lines.Add($"User={user}");
    lines.Add($"State={state}");
    lines.Add("Active=yes");

    return string.Join('\n', lines);
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
    _processManager
      .Setup(x => x.GetProcessOutputStrict("loginctl", $"show-session {sessionId}", It.IsAny<int>()))
      .ReturnsAsync(Result.Ok(sessionOutput));
  }

  private void SetupSessions(params string[] sessionIds)
  {
    var output = string.Join('\n', sessionIds);
    _processManager
      .Setup(x => x.GetProcessOutput("loginctl", "list-sessions --no-legend", It.IsAny<int>()))
      .ReturnsAsync(Result.Ok(output));
    _processManager
      .Setup(x => x.GetProcessOutputStrict("loginctl", "list-sessions --no-legend", It.IsAny<int>()))
      .ReturnsAsync(Result.Ok(output));
  }

  private void SetupSessionsFailure()
  {
    // loginctl exits 1 with an empty stdout, so the lenient probe sees a successful
    // empty result while the strict probe reports the failure.
    _processManager
      .Setup(x => x.GetProcessOutput("loginctl", "list-sessions --no-legend", It.IsAny<int>()))
      .ReturnsAsync(Result.Ok(""));
    _processManager
      .Setup(x => x.GetProcessOutputStrict("loginctl", "list-sessions --no-legend", It.IsAny<int>()))
      .ReturnsAsync(Result.Fail<string>("loginctl list-sessions --no-legend exited with code 1."));
  }
}
