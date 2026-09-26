using ControlR.Agent.Shared.Options;
using ControlR.Agent.Shared.Services.Linux;
using ControlR.Libraries.Shared.Services.Processes;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ControlR.Agent.Shared.Tests;

/// <summary>
/// Pins the #157 split: only an active session gets a desktop client launched, but every
/// logged-in user (active or not) gets one stopped, so a backgrounded session is not orphaned.
/// </summary>
public class DesktopClientSessionPolicyTests
{
  private const string ActiveUid = "1000";
  private const string InactiveUid = "2000";

  [Fact]
  public async Task StartDesktopClientService_LaunchesOnlyForActiveUser()
  {
    var processManager = new Mock<IProcessManager>();
    var sut = CreateSut(processManager);

    await sut.StartDesktopClientService(throwOnFailure: false);

    AssertStartedFor(processManager, ActiveUid);
    AssertNotStartedFor(processManager, InactiveUid);
  }

  [Fact]
  public async Task StopDesktopClientService_StopsForActiveAndInactiveUsers()
  {
    var processManager = new Mock<IProcessManager>();
    var sut = CreateSut(processManager);

    await sut.StopDesktopClientService(throwOnFailure: false);

    AssertStoppedFor(processManager, ActiveUid);
    AssertStoppedFor(processManager, InactiveUid);
  }

  private static void AssertNotStartedFor(Mock<IProcessManager> processManager, string uid)
  {
    processManager.Verify(
      x => x.StartAndWaitForExit(
        It.IsAny<string>(),
        It.Is<string>(args => args.Contains($"-u #{uid}") && args.Contains("systemctl --user start")),
        It.IsAny<bool>(),
        It.IsAny<TimeSpan>()),
      Times.Never);
  }

  private static void AssertStartedFor(Mock<IProcessManager> processManager, string uid)
  {
    processManager.Verify(
      x => x.StartAndWaitForExit(
        "sudo",
        It.Is<string>(args => args.Contains($"-u #{uid}") && args.Contains("systemctl --user start")),
        It.IsAny<bool>(),
        It.IsAny<TimeSpan>()),
      Times.Once);
  }

  private static void AssertStoppedFor(Mock<IProcessManager> processManager, string uid)
  {
    processManager.Verify(
      x => x.StartAndWaitForExit(
        "sudo",
        It.Is<string>(args => args.Contains($"-u #{uid}") && args.Contains("systemctl --user stop")),
        It.IsAny<bool>(),
        It.IsAny<TimeSpan>()),
      Times.Once);
  }

  private static ServiceControlLinux CreateSut(Mock<IProcessManager> processManager)
  {
    processManager
      .Setup(x => x.StartAndWaitForExit(
        It.IsAny<string>(),
        It.IsAny<string>(),
        It.IsAny<bool>(),
        It.IsAny<TimeSpan>()))
      .ReturnsAsync(0);

    var headless = new Mock<IHeadlessServerDetector>();
    headless.Setup(x => x.IsHeadlessServer()).ReturnsAsync(false);

    var provider = new Mock<ILoggedInUserProvider>();
    provider
      .Setup(x => x.GetLoggedInUsers())
      .ReturnsAsync(
      [
        new LoggedInUserSession(ActiveUid, "wayland", true),
        new LoggedInUserSession(InactiveUid, "wayland", false)
      ]);

    return new ServiceControlLinux(
      processManager.Object,
      headless.Object,
      provider.Object,
      Microsoft.Extensions.Options.Options.Create(new InstanceOptions()),
      NullLogger<ServiceControlLinux>.Instance);
  }
}
