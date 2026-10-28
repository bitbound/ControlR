using ControlR.Agent.Common.Interfaces;
using ControlR.Agent.Common.Services;
using ControlR.Agent.Common.Services.Linux;
using ControlR.Agent.Shared.Interfaces;
using ControlR.Agent.Shared.Services;
using ControlR.Agent.Shared.Services.Linux;
using ControlR.Libraries.Shared.Constants;
using ControlR.Libraries.Shared.Primitives;
using ControlR.Libraries.Shared.Services;
using ControlR.Libraries.Shared.Services.FileSystem;
using ControlR.Libraries.Shared.Services.Processes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using System.Diagnostics;

namespace ControlR.Agent.Common.Tests;

public class DesktopClientWatcherLinuxTests
{
  private readonly Mock<IDesktopEnvironmentDetectorAgent> _detector = new();
  private readonly Mock<IFileSystem> _fileSystem = new();
  private readonly Mock<IFileSystemPathProvider> _fileSystemPathProvider = new();
  private readonly Mock<ILinuxDesktopClientProcessLocator> _processLocator = new();
  private readonly Mock<IProcessManager> _processManager = new();
  private readonly FakeTimeProvider _timeProvider = new(DateTimeOffset.UtcNow);

  [Fact]
  public async Task CheckLoginScreenDesktopClient_KillsStaleOrphanBeforeSpawning()
  {
    var running = CreateRunningProcess();
    SetupLaunch(running);
    var sut = CreateSut();

    var stale = new Mock<IProcess>();
    _processLocator
      .Setup(x => x.FindByExecutablePath(It.IsAny<string>()))
      .Returns([new LinuxDesktopClientProcess(999, 0, "/opt/controlr/DesktopClient/ControlR.DesktopClient (deleted)")]);
    _processLocator
      .Setup(x => x.GetRunningExecutableOwnerUid(999, It.IsAny<string>()))
      .Returns(0);
    _processManager
      .Setup(x => x.GetProcessById(999))
      .Returns(stale.Object);

    _detector
      .Setup(x => x.DetectDisplayEnvironment())
      .ReturnsAsync(LoginScreen());
    await sut.CheckLoginScreenDesktopClient(CancellationToken.None);

    stale.Verify(x => x.Kill(), Times.Once);
    _processManager.Verify(x => x.Start(It.IsAny<ProcessStartInfo>()), Times.Once);
  }

  [Fact]
  public async Task CheckLoginScreenDesktopClient_LaunchPassesAgentPidToClient()
  {
    // The greeter client is the only client the agent spawns as a direct child, and on Linux a
    // child outlives its parent. The watchdog in the client needs to know which pid to watch for.
    var running = CreateRunningProcess();
    ProcessStartInfo? launched = null;
    _processManager
      .Setup(x => x.Start(It.IsAny<ProcessStartInfo>()))
      .Callback<ProcessStartInfo>(startInfo => launched = startInfo)
      .Returns(running.Object);
    var sut = CreateSut();

    _detector
      .Setup(x => x.DetectDisplayEnvironment())
      .ReturnsAsync(LoginScreen());
    await sut.CheckLoginScreenDesktopClient(CancellationToken.None);

    Assert.NotNull(launched);
    Assert.True(
      launched!.Environment.TryGetValue(AppConstants.AgentPidEnvironmentVariable, out var agentPid),
      "The launch environment is missing the agent pid variable.");
    Assert.Equal(Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture), agentPid);
  }

  [Fact]
  public async Task CheckLoginScreenDesktopClient_ReconcilesWhileTrackedClientIsHealthy()
  {
    // A launch attempt that loses the race to record itself leaves a greeter client this agent never
    // tracked. The launch path is not reached while the recorded client still looks healthy, so the
    // reconcile has to run on those passes too or the duplicate sits beside it indefinitely.
    var running = CreateRunningProcess();
    SetupLaunch(running);
    var sut = CreateSut();

    _detector
      .Setup(x => x.DetectDisplayEnvironment())
      .ReturnsAsync(LoginScreen());
    await sut.CheckLoginScreenDesktopClient(CancellationToken.None);

    var duplicate = new Mock<IProcess>();
    _processLocator
      .Setup(x => x.FindByExecutablePath(It.IsAny<string>()))
      .Returns([
        new LinuxDesktopClientProcess(101, 0, "/opt/controlr/DesktopClient/ControlR.DesktopClient"),
        new LinuxDesktopClientProcess(999, 0, "/opt/controlr/DesktopClient/ControlR.DesktopClient")]);
    _processLocator
      .Setup(x => x.GetRunningExecutableOwnerUid(999, It.IsAny<string>()))
      .Returns(0);
    _processManager
      .Setup(x => x.GetProcessById(999))
      .Returns(duplicate.Object);

    _timeProvider.Advance(TimeSpan.FromSeconds(5));
    await sut.CheckLoginScreenDesktopClient(CancellationToken.None);

    duplicate.Verify(x => x.Kill(), Times.Once);
    running.Verify(x => x.Kill(), Times.Never);
    _processManager.Verify(x => x.Start(It.IsAny<ProcessStartInfo>()), Times.Once);
  }

  [Fact]
  public async Task CheckLoginScreenDesktopClient_WhenDetectionIsInconclusive_LeavesRunningClientAlone()
  {
    var process = CreateRunningProcess();
    SetupLaunch(process);
    var sut = CreateSut();

    _detector
      .Setup(x => x.DetectDisplayEnvironment())
      .ReturnsAsync(LoginScreen());
    await sut.CheckLoginScreenDesktopClient(CancellationToken.None);

    _detector
      .Setup(x => x.DetectDisplayEnvironment())
      .ReturnsAsync(new DisplayEnvironmentInfo { IsConclusive = false });
    _timeProvider.Advance(TimeSpan.FromSeconds(5));
    await sut.CheckLoginScreenDesktopClient(CancellationToken.None);

    _processManager.Verify(x => x.Start(It.IsAny<ProcessStartInfo>()), Times.Once);
    process.Verify(x => x.Kill(), Times.Never);
  }

  [Fact]
  public async Task CheckLoginScreenDesktopClient_WhenLaunchesExitQuickly_BacksOff()
  {
    SetupLaunch(CreateExitedProcess());
    var sut = CreateSut();

    _detector
      .Setup(x => x.DetectDisplayEnvironment())
      .ReturnsAsync(LoginScreen());
    await sut.CheckLoginScreenDesktopClient(CancellationToken.None);
    _processManager.Verify(x => x.Start(It.IsAny<ProcessStartInfo>()), Times.Once);

    _timeProvider.Advance(TimeSpan.FromSeconds(5));
    await sut.CheckLoginScreenDesktopClient(CancellationToken.None);
    _processManager.Verify(x => x.Start(It.IsAny<ProcessStartInfo>()), Times.Once);

    _timeProvider.Advance(TimeSpan.FromSeconds(10));
    await sut.CheckLoginScreenDesktopClient(CancellationToken.None);
    _processManager.Verify(x => x.Start(It.IsAny<ProcessStartInfo>()), Times.Exactly(2));

    _timeProvider.Advance(TimeSpan.FromSeconds(5));
    await sut.CheckLoginScreenDesktopClient(CancellationToken.None);
    _processManager.Verify(x => x.Start(It.IsAny<ProcessStartInfo>()), Times.Exactly(2));

    _timeProvider.Advance(TimeSpan.FromSeconds(15));
    await sut.CheckLoginScreenDesktopClient(CancellationToken.None);
    _processManager.Verify(x => x.Start(It.IsAny<ProcessStartInfo>()), Times.Exactly(3));
  }

  [Fact]
  public async Task CheckLoginScreenDesktopClient_WhenLoginScreenEnds_ClearsBackoff()
  {
    SetupLaunch(CreateExitedProcess());
    var sut = CreateSut();

    _detector
      .Setup(x => x.DetectDisplayEnvironment())
      .ReturnsAsync(LoginScreen());
    await sut.CheckLoginScreenDesktopClient(CancellationToken.None);

    _detector
      .Setup(x => x.DetectDisplayEnvironment())
      .ReturnsAsync(new DisplayEnvironmentInfo { IsConclusive = true, IsLoginScreen = false });
    _timeProvider.Advance(TimeSpan.FromSeconds(5));
    await sut.CheckLoginScreenDesktopClient(CancellationToken.None);

    _detector
      .Setup(x => x.DetectDisplayEnvironment())
      .ReturnsAsync(LoginScreen());
    await sut.CheckLoginScreenDesktopClient(CancellationToken.None);

    _processManager.Verify(x => x.Start(It.IsAny<ProcessStartInfo>()), Times.Exactly(2));
  }

  [Fact]
  public async Task CheckLoginScreenDesktopClient_WhenNotAtLoginScreen_ReconcilesUntrackedClient()
  {
    // An untracked root-owned greeter client must not survive the greeter into the user's session.
    // Once the greeter is gone no later pass reconciles, so the non-login-screen path has to reap it.
    var sut = CreateSut();

    var orphan = new Mock<IProcess>();
    _processLocator
      .Setup(x => x.FindByExecutablePath(It.IsAny<string>()))
      .Returns([new LinuxDesktopClientProcess(999, 0, "/opt/controlr/DesktopClient/ControlR.DesktopClient")]);
    _processLocator
      .Setup(x => x.GetRunningExecutableOwnerUid(999, It.IsAny<string>()))
      .Returns(0);
    _processManager
      .Setup(x => x.GetProcessById(999))
      .Returns(orphan.Object);

    _detector
      .Setup(x => x.DetectDisplayEnvironment())
      .ReturnsAsync(new DisplayEnvironmentInfo { IsConclusive = true, IsLoginScreen = false });
    await sut.CheckLoginScreenDesktopClient(CancellationToken.None);

    orphan.Verify(x => x.Kill(), Times.Once);
  }

  [Fact]
  public async Task CheckLoginScreenDesktopClient_WhenStoppedForLogout_DoesNotApplyLaunchBackoff()
  {
    SetupLaunch(CreateRunningProcess());
    var sut = CreateSut();

    _detector
      .Setup(x => x.DetectDisplayEnvironment())
      .ReturnsAsync(LoginScreen());
    await sut.CheckLoginScreenDesktopClient(CancellationToken.None);

    _detector
      .Setup(x => x.DetectDisplayEnvironment())
      .ReturnsAsync(new DisplayEnvironmentInfo { IsConclusive = true, IsLoginScreen = false });
    _timeProvider.Advance(TimeSpan.FromSeconds(5));
    await sut.CheckLoginScreenDesktopClient(CancellationToken.None);

    _detector
      .Setup(x => x.DetectDisplayEnvironment())
      .ReturnsAsync(LoginScreen());
    _timeProvider.Advance(TimeSpan.FromSeconds(5));
    await sut.CheckLoginScreenDesktopClient(CancellationToken.None);

    _processManager.Verify(x => x.Start(It.IsAny<ProcessStartInfo>()), Times.Exactly(2));
  }

  [Fact]
  public async Task CheckLoginScreenDesktopClient_WhenXAuthPathIsMissing_DoesNotLaunch()
  {
    var sut = CreateSut();

    _detector
      .Setup(x => x.DetectDisplayEnvironment())
      .ReturnsAsync(new DisplayEnvironmentInfo
      {
        IsConclusive = true,
        IsLoginScreen = true,
        Display = ":0",
        DisplayManager = "lightdm"
      });
    await sut.CheckLoginScreenDesktopClient(CancellationToken.None);

    _processManager.Verify(x => x.Start(It.IsAny<ProcessStartInfo>()), Times.Never);
  }

  [Fact]
  public void ReconcileGreeterDesktopClients_KillsStaleRootOwnedClient()
  {
    var sut = CreateSut();

    var stale = new Mock<IProcess>();
    _processLocator
      .Setup(x => x.FindByExecutablePath(It.IsAny<string>()))
      .Returns([new LinuxDesktopClientProcess(999, 0, "/opt/controlr/DesktopClient/ControlR.DesktopClient (deleted)")]);
    _processLocator
      .Setup(x => x.GetRunningExecutableOwnerUid(999, It.IsAny<string>()))
      .Returns(0);
    _processManager
      .Setup(x => x.GetProcessById(999))
      .Returns(stale.Object);

    sut.ReconcileGreeterDesktopClients();

    stale.Verify(x => x.Kill(), Times.Once);
  }

  [Fact]
  public async Task ReconcileGreeterDesktopClients_SparesTrackedClient()
  {
    var running = CreateRunningProcess();
    SetupLaunch(running);
    var sut = CreateSut();

    _detector
      .Setup(x => x.DetectDisplayEnvironment())
      .ReturnsAsync(LoginScreen());
    await sut.CheckLoginScreenDesktopClient(CancellationToken.None);

    _processLocator
      .Setup(x => x.FindByExecutablePath(It.IsAny<string>()))
      .Returns([new LinuxDesktopClientProcess(101, 0, "/opt/controlr/DesktopClient/ControlR.DesktopClient")]);

    sut.ReconcileGreeterDesktopClients();

    running.Verify(x => x.Kill(), Times.Never);
    _processManager.Verify(x => x.GetProcessById(101), Times.Never);
  }

  [Fact]
  public void ReconcileGreeterDesktopClients_SparesUserSessionClient()
  {
    var sut = CreateSut();

    // A user session client runs the same binary but under the user's uid, and belongs to the user's
    // systemd unit, so the greeter reconcile must leave it alone.
    _processLocator
      .Setup(x => x.FindByExecutablePath(It.IsAny<string>()))
      .Returns([new LinuxDesktopClientProcess(555, 1000, "/opt/controlr/DesktopClient/ControlR.DesktopClient")]);

    sut.ReconcileGreeterDesktopClients();

    _processManager.Verify(x => x.GetProcessById(It.IsAny<int>()), Times.Never);
  }

  [Theory]
  [InlineData(1000)]
  [InlineData(-1)]
  public void ReconcileGreeterDesktopClients_WhenRecheckDoesNotReportRoot_SparesProcess(int recheckedOwnerUid)
  {
    var sut = CreateSut();

    // The owner is read when the process is found and read again before the kill. A pid recycled in
    // that window to a user session client of the same binary must not be killed on the strength of
    // the earlier root ownership, and an owner that cannot be read fails closed too.
    _processLocator
      .Setup(x => x.FindByExecutablePath(It.IsAny<string>()))
      .Returns([new LinuxDesktopClientProcess(999, 0, "/opt/controlr/DesktopClient/ControlR.DesktopClient")]);
    _processLocator
      .Setup(x => x.GetRunningExecutableOwnerUid(999, It.IsAny<string>()))
      .Returns(recheckedOwnerUid);

    sut.ReconcileGreeterDesktopClients();

    _processManager.Verify(x => x.GetProcessById(It.IsAny<int>()), Times.Never);
  }

  [Fact]
  public async Task StopClientsForShutdownAsync_KillsTrackedLoginScreenClient()
  {
    var running = CreateRunningProcess();
    SetupLaunch(running);
    var sut = CreateSut();

    _detector
      .Setup(x => x.DetectDisplayEnvironment())
      .ReturnsAsync(LoginScreen());
    await sut.CheckLoginScreenDesktopClient(CancellationToken.None);

    await sut.StopClientsForShutdownAsync();

    // AtLeastOnce, not Once: stopping the client also completes the launch task, whose finally block
    // disposes the same process again. The fix is that shutdown kills it at all; before it, the
    // tracked client survived and Kill was never called.
    running.Verify(x => x.Kill(), Times.AtLeastOnce);
  }

  private static Mock<IProcess> CreateExitedProcess()
  {
    var process = new Mock<IProcess>();
    process.SetupGet(x => x.HasExited).Returns(true);
    process.SetupGet(x => x.Id).Returns(101);
    process.Setup(x => x.WaitForExitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
    return process;
  }

  private static Mock<IProcess> CreateRunningProcess()
  {
    var exit = new TaskCompletionSource();
    var process = new Mock<IProcess>();
    process.SetupGet(x => x.HasExited).Returns(() => exit.Task.IsCompleted);
    process.SetupGet(x => x.Id).Returns(101);
    process.Setup(x => x.WaitForExitAsync(It.IsAny<CancellationToken>())).Returns(exit.Task);
    process.Setup(x => x.Kill()).Callback(() => exit.TrySetResult());
    return process;
  }

  private static DisplayEnvironmentInfo LoginScreen()
  {
    return new DisplayEnvironmentInfo
    {
      Display = ":0",
      DisplayManager = "lightdm",
      IsConclusive = true,
      IsLoginScreen = true,
      XAuthPath = "/run/lightdm/root/:0"
    };
  }

  private DesktopClientWatcherLinux CreateSut()
  {
    _fileSystemPathProvider
      .Setup(x => x.GetAgentInstallDirectory())
      .Returns("/opt/controlr");
    _fileSystem
      .Setup(x => x.FileExists(It.IsAny<string>()))
      .Returns(true);
    _processManager
      .Setup(x => x.GetProcessOutput("readlink", It.IsAny<string>(), It.IsAny<int>()))
      .ReturnsAsync(Result.Ok("/opt/controlr/DesktopClient/ControlR.DesktopClient"));
    _processLocator
      .Setup(x => x.FindByExecutablePath(It.IsAny<string>()))
      .Returns([]);

    return new DesktopClientWatcherLinux(
      _timeProvider,
      Mock.Of<IServiceControl>(),
      Mock.Of<IDesktopClientRepairCoordinator>(),
      _detector.Object,
      Mock.Of<IHeadlessServerDetector>(),
      Mock.Of<ISystemEnvironment>(),
      _fileSystem.Object,
      Mock.Of<IDesktopClientFileVerifier>(),
      Mock.Of<ILoggedInUserProvider>(),
      _processManager.Object,
      _fileSystemPathProvider.Object,
      _processLocator.Object,
      NullLogger<DesktopClientWatcherLinux>.Instance);
  }

  private void SetupLaunch(Mock<IProcess> process)
  {
    _processManager
      .Setup(x => x.Start(It.IsAny<ProcessStartInfo>()))
      .Returns(process.Object);
  }
}
