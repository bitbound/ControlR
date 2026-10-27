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
  public void FindByExecutablePath_WhenPathIsEmpty_ReturnsNothing()
  {
    var sut = CreateSut();

    Assert.Empty(sut.FindByExecutablePath(string.Empty));
    _fileSystem.Verify(x => x.GetDirectories(It.IsAny<string>()), Times.Never);
  }

  [Fact]
  public void IsRunningExecutable_RechecksASingleProcess()
  {
    var sut = CreateSut();
    SetupExecutable(100, TargetPath);
    SetupExecutable(200, "/usr/bin/bash");

    Assert.True(sut.IsRunningExecutable(100, TargetPath));
    Assert.False(sut.IsRunningExecutable(200, TargetPath));
  }

  private LinuxDesktopClientProcessLocator CreateSut()
  {
    return new LinuxDesktopClientProcessLocator(
      _fileSystem.Object,
      NullLogger<LinuxDesktopClientProcessLocator>.Instance);
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
