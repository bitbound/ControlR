using System.Runtime.Versioning;
using ControlR.Agent.Common.Interfaces;
using ControlR.Agent.Common.Services;
using ControlR.Agent.Common.Services.Windows;
using ControlR.Libraries.Api.Contracts.Dtos.Devices;
using ControlR.Libraries.NativeInterop.Windows;
using ControlR.Libraries.Shared.Services;
using ControlR.Libraries.Shared.Services.FileSystem;
using ControlR.Libraries.Shared.Services.Processes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace ControlR.Agent.Common.Tests.Services;

[SupportedOSPlatform("windows6.0.6000")]
public class DesktopClientWatcherWinTests
{
  private readonly Mock<IDesktopClientUpdater> _desktopClientUpdater = new();
  private readonly Mock<IDesktopSessionProvider> _desktopSessionProvider = new();
  private readonly Mock<ISystemEnvironment> _environment = new();
  private readonly Mock<IFileSystem> _fileSystem = new();
  private readonly Mock<IIpcServerStore> _ipcServerStore = new();
  private readonly ILogger<DesktopClientLaunchTracker> _launchTrackerLogger = new NullLogger<DesktopClientLaunchTracker>();
  private readonly Mock<ILogger<DesktopClientWatcherWin>> _logger = new();
  private readonly Mock<IControlrMutationLock> _mutationLock = new();
  private readonly Mock<IProcessManager> _processManager = new();
  private readonly Mock<ISettingsProvider> _settingsProvider = new();
  private readonly FakeTimeProvider _timeProvider = new(DateTimeOffset.UtcNow);
  private readonly Mock<IWaiter> _waiter = new();
  private readonly Mock<IWin32Interop> _win32Interop = new();

  [Fact]
  public void Reconcile_WhenGraceWindowExpires_RemovesTrackedLaunch()
  {
    HashSet<int> activeSessionIds = [5];
    var tracker = new DesktopClientLaunchTracker(_timeProvider, _launchTrackerLogger);
    var process = CreateProcess(processId: 101, sessionId: 5, hasExited: false);

    tracker.TrackLaunch(5, process.Object);
    _timeProvider.Advance(DesktopClientLaunchTracker.StartupGracePeriod + TimeSpan.FromSeconds(1));

    tracker.Reconcile(activeSessionIds, []);

    Assert.Equal(0, tracker.Count);
    Assert.False(tracker.IsSessionCovered(5, []));
  }

  [Fact]
  public void Reconcile_WhenIpcRegistrationObserved_ClearsTrackedLaunch()
  {
    HashSet<int> activeSessionIds = [5];
    var tracker = new DesktopClientLaunchTracker(_timeProvider, _launchTrackerLogger);
    var process = CreateProcess(processId: 101, sessionId: 5, hasExited: false);

    tracker.TrackLaunch(5, process.Object);
    tracker.Reconcile(
      activeSessionIds,
      [new DesktopSession { ProcessId = 202, SystemSessionId = 5 }]);

    Assert.Equal(0, tracker.Count);
  }

  [Fact]
  public void Reconcile_WhenTrackedProcessExits_RemovesTrackedLaunch()
  {
    HashSet<int> activeSessionIds = [5];
    var hasExited = false;
    var tracker = new DesktopClientLaunchTracker(_timeProvider, _launchTrackerLogger);
    var process = CreateProcess(processId: 101, sessionId: 5, hasExited: () => hasExited);

    tracker.TrackLaunch(5, process.Object);
    hasExited = true;

    tracker.Reconcile(activeSessionIds, []);

    Assert.Equal(0, tracker.Count);
    Assert.False(tracker.IsSessionCovered(5, []));
  }

  [Fact]
  public async Task RunIteration_WhenLaunchFailsImmediately_IncrementsFailureCount()
  {
    using var mutationLock = Mock.Of<IDisposable>();
    IProcess? startedProcess = null;
    var watcher = CreateWatcher();

    _desktopSessionProvider
      .Setup(x => x.GetActiveDesktopClients())
      .ReturnsAsync([]);
    _desktopClientUpdater
      .Setup(x => x.EnsureLatestVersion(false, It.IsAny<CancellationToken>()))
      .ReturnsAsync(true);
    _mutationLock
      .Setup(x => x.AcquireAsync(It.IsAny<CancellationToken>()))
      .ReturnsAsync(mutationLock);
    _win32Interop
      .Setup(x => x.CreateInteractiveSystemProcess(It.IsAny<string>(), 5, true, out startedProcess))
      .Returns(false);

    await watcher.RunIteration(
      [new DesktopSession { SystemSessionId = 5 }],
      [],
      CancellationToken.None);

    Assert.Equal(1, watcher.LaunchFailCount);
  }

  [Fact]
  public void TrackLaunch_MarksSessionCoveredBeforeRegistration()
  {
    var tracker = new DesktopClientLaunchTracker(_timeProvider, _launchTrackerLogger);
    var process = CreateProcess(processId: 101, sessionId: 5, hasExited: false);

    tracker.TrackLaunch(5, process.Object);

    Assert.True(tracker.IsSessionCovered(5, []));
  }

  private static Mock<IProcess> CreateProcess(int processId, int sessionId, bool hasExited)
  {
    return CreateProcess(processId, sessionId, () => hasExited);
  }

  private static Mock<IProcess> CreateProcess(int processId, int sessionId, Func<bool> hasExited)
  {
    var process = new Mock<IProcess>();
    process.SetupGet(x => x.Id).Returns(processId);
    process.SetupGet(x => x.SessionId).Returns(sessionId);
    process.SetupGet(x => x.HasExited).Returns(() => hasExited());
    return process;
  }

  private DesktopClientWatcherWin CreateWatcher()
  {
    var launchTracker = new DesktopClientLaunchTracker(
      _timeProvider,
      _launchTrackerLogger);

    _environment.SetupGet(x => x.IsDebug).Returns(false);
    _environment.SetupGet(x => x.StartupDirectory).Returns("C:\\ControlR");
    _settingsProvider.SetupGet(x => x.InstanceId).Returns("instance-1");
    _waiter
      .Setup(x => x.WaitFor(
        It.IsAny<Func<bool>>(),
        It.IsAny<TimeSpan?>(),
        It.IsAny<Func<Task>?>(),
        It.IsAny<bool>(),
        It.IsAny<CancellationToken>()))
      .ReturnsAsync(false);

    return new DesktopClientWatcherWin(
      _timeProvider,
      _win32Interop.Object,
      _processManager.Object,
      _ipcServerStore.Object,
      _environment.Object,
      _fileSystem.Object,
      _settingsProvider.Object,
      _mutationLock.Object,
      _desktopSessionProvider.Object,
      _desktopClientUpdater.Object,
      launchTracker,
      _waiter.Object,
      _logger.Object);
  }
}
