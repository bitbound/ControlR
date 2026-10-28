using System.Runtime.Versioning;
using ControlR.Agent.Shared.Services.Linux;
using ControlR.Libraries.Shared.Services.FileSystem;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ControlR.Agent.Shared.Tests;

[SupportedOSPlatform("linux")]
public class LinuxDesktopClientProcessLocatorTests
{
  private const string TargetPath = "/usr/local/bin/controlr/default/DesktopClient/ControlR.DesktopClient";

  private readonly Mock<IFileSystem> _fileSystem = new();

  [Fact]
  public void FindByExecutablePath_IgnoresOtherExecutablesAndNonNumericEntries()
  {
    var sut = CreateSut();
    SetupProcDirectories("/proc/100", "/proc/200", "/proc/self");
    SetupExecutable(100, TargetPath);
    SetupExecutable(200, "/usr/bin/bash");
    SetupStatusUid(100, 0);

    var result = sut.FindByExecutablePath(TargetPath);

    var match = Assert.Single(result);
    Assert.Equal(100, match.ProcessId);

    // A non-numeric /proc entry is not a process, so its exe is never read.
    _fileSystem.Verify(x => x.ResolveLinkTarget("/proc/self/exe", false), Times.Never);
  }

  [Fact]
  public void FindByExecutablePath_MatchesABinaryReplacedOnDisk()
  {
    // The kernel appends " (deleted)" to /proc/{pid}/exe once the file is replaced, which is exactly
    // the state of a client orphaned by an update. It still has to match the path it launched from.
    var sut = CreateSut();
    SetupProcDirectories("/proc/100");
    SetupExecutable(100, $"{TargetPath} (deleted)");
    SetupStatusUid(100, 0);

    var result = sut.FindByExecutablePath(TargetPath);

    var match = Assert.Single(result);
    Assert.Equal(100, match.ProcessId);
    Assert.Equal(0, match.OwnerUid);
  }

  [Fact]
  public void FindByExecutablePath_MatchesALiveBinary()
  {
    var sut = CreateSut();
    SetupProcDirectories("/proc/100");
    SetupExecutable(100, TargetPath);
    SetupStatusUid(100, 0);

    var result = sut.FindByExecutablePath(TargetPath);

    Assert.Equal(100, Assert.Single(result).ProcessId);
  }

  [Theory]
  [InlineData("0::/system.slice/controlr-agent.service", false)]
  [InlineData("0::/user.slice/user-0.slice/user@0.service/app.slice/app-controlr.service", true)]
  [InlineData("12:cpu,cpuacct:/system.slice/controlr-agent.service\n0::/system.slice/controlr-agent.service", false)]
  public void FindByExecutablePath_ReportsTheCgroupSlice(string cgroup, bool expectedInUserSlice)
  {
    // cgroup v2 yields one line; v1 yields one line per controller. A user session's systemd path
    // lives under user.slice in either.
    var sut = CreateSut();
    SetupProcDirectories("/proc/100");
    SetupExecutable(100, TargetPath);
    SetupStatusUid(100, 0);
    SetupCgroup(100, cgroup);

    var match = Assert.Single(sut.FindByExecutablePath(TargetPath));
    Assert.Equal(expectedInUserSlice, match.InUserSlice);
  }

  [Fact]
  public void FindByExecutablePath_ReportsTheOwnerUid()
  {
    var sut = CreateSut();
    SetupProcDirectories("/proc/100");
    SetupExecutable(100, TargetPath);
    SetupStatusUid(100, 1000);

    var result = sut.FindByExecutablePath(TargetPath);

    Assert.Equal(1000, Assert.Single(result).OwnerUid);
  }

  [Fact]
  public void FindByExecutablePath_WhenCgroupUnreadable_ReportsUserSlice()
  {
    // Only a proven system-slice process may be killed as a greeter orphan, so an unreadable
    // cgroup claims the safer user-slice side.
    var sut = CreateSut();
    SetupProcDirectories("/proc/100");
    SetupExecutable(100, TargetPath);
    SetupStatusUid(100, 0);
    _fileSystem
      .Setup(x => x.ReadAllText("/proc/100/cgroup"))
      .Throws(new UnauthorizedAccessException());

    var match = Assert.Single(sut.FindByExecutablePath(TargetPath));
    Assert.True(match.InUserSlice);
  }

  [Fact]
  public void FindByExecutablePath_WhenPathIsEmpty_ReturnsNothing()
  {
    var sut = CreateSut();

    Assert.Empty(sut.FindByExecutablePath(string.Empty));
    _fileSystem.Verify(x => x.GetDirectories(It.IsAny<string>()), Times.Never);
  }

  [Fact]
  public void GetRunningExecutable_ReportsTheCurrentOwner()
  {
    var sut = CreateSut();
    SetupExecutable(100, TargetPath);
    SetupExecutable(200, "/usr/bin/bash");
    SetupStatusUid(100, 1000);
    SetupCgroup(100, "0::/user.slice/user-1000.slice/user@1000.service/app.slice/app-controlr.service");

    // A pid found earlier can be recycled before the kill, so the re-check reports who owns the
    // process now instead of only whether it still runs the expected binary.
    var match = sut.GetRunningExecutable(100, TargetPath);
    Assert.NotNull(match);
    Assert.Equal(1000, match.Value.OwnerUid);
    Assert.True(match.Value.InUserSlice);
    Assert.Null(sut.GetRunningExecutable(200, TargetPath));
  }

  [Fact]
  public void GetRunningExecutable_WhenNothingMatches_ReturnsNothing()
  {
    var sut = CreateSut();
    SetupExecutable(100, TargetPath);

    Assert.Null(sut.GetRunningExecutable(100, $"{TargetPath}x"));
    Assert.Null(sut.GetRunningExecutable(100, string.Empty));
    _fileSystem.Verify(x => x.ReadAllText(It.IsAny<string>()), Times.Never);
  }

  private LinuxDesktopClientProcessLocator CreateSut()
  {
    return new LinuxDesktopClientProcessLocator(
      _fileSystem.Object,
      NullLogger<LinuxDesktopClientProcessLocator>.Instance);
  }

  private void SetupCgroup(int processId, string cgroup)
  {
    _fileSystem
      .Setup(x => x.ReadAllText($"/proc/{processId}/cgroup"))
      .Returns(cgroup);
  }

  private void SetupExecutable(int processId, string resolvedPath)
  {
    _fileSystem
      .Setup(x => x.ResolveLinkTarget($"/proc/{processId}/exe", false))
      .Returns(Mock.Of<IFileSystemItemInfo>(info => info.FullName == resolvedPath));
  }

  private void SetupProcDirectories(params string[] directories)
  {
    _fileSystem
      .Setup(x => x.GetDirectories("/proc"))
      .Returns(directories);
  }

  private void SetupStatusUid(int processId, int uid)
  {
    _fileSystem
      .Setup(x => x.ReadAllText($"/proc/{processId}/status"))
      .Returns($"Name:\tControlR.Deskto\nUmask:\t0022\nUid:\t{uid}\t{uid}\t{uid}\t{uid}\nGid:\t{uid}\t{uid}\t{uid}\t{uid}\n");
  }
}
