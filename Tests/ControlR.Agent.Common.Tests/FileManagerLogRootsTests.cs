using ControlR.Agent.Common.Services.FileManager;
using ControlR.Agent.Shared.Services;
using ControlR.Libraries.Shared.Services.FileSystem;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ControlR.Agent.Common.Tests;

/// <summary>
/// Pins that the log-contents stream is confined to the log roots the agent itself enumerates, so
/// log-read on one device cannot be aimed at an arbitrary file the agent process can open (#260).
/// </summary>
public class FileManagerLogRootsTests
{
  private const string AgentLogs = "/var/log/controlr-agent";
  private const string AliceDesktopClientLogs = "/home/alice/.local/share/controlr/logs";
  private const string InstallerLogs = "/var/log/controlr-installer";
  private const string RootDesktopClientLogs = "/root/.local/share/controlr/logs";

  [Theory]
  [InlineData("/etc/shadow")]
  [InlineData("/home/alice/.ssh/id_rsa")]
  [InlineData("/home/alice/.local/share/controlr/logs/../../.ssh/id_rsa")]
  [InlineData("/var/log/controlr-agentevil/LogFile20260101.log")]
  [InlineData("")]
  public void IsPathWithinLogRoots_WhenOutsideTheLogRoots_ReturnsFalse(string path)
  {
    Assert.False(CreateManager().IsPathWithinLogRoots(path));
  }

  [Fact]
  public void IsPathWithinLogRoots_WhenSymlinkInsideRootLeavesTheRoot_ReturnsFalse()
  {
    var rootDir = Path.Combine(Path.GetTempPath(), "controlr-logroot-" + Guid.NewGuid().ToString("N"));
    var outsideFile = Path.Combine(Path.GetTempPath(), "controlr-outside-" + Guid.NewGuid().ToString("N") + ".txt");
    Directory.CreateDirectory(rootDir);
    File.WriteAllText(outsideFile, "secret");

    try
    {
      var linkPath = Path.Combine(rootDir, "LogFile20260101.log");
      try
      {
        File.CreateSymbolicLink(linkPath, outsideFile);
      }
      catch (Exception)
      {
        // Symlink creation needs SeCreateSymbolicLinkPrivilege or developer mode
        // on Windows. The Linux CI job is the one that runs this project, so the
        // test silently passes the privilege gap on hosts that cannot plant a link.
        return;
      }

      Assert.False(CreateManagerWithRoot(rootDir).IsPathWithinLogRoots(linkPath));
    }
    finally
    {
      TryDeleteFile(Path.Combine(rootDir, "LogFile20260101.log"), rootDir);
      TryDeleteFile(outsideFile, null);
    }
  }

  [Fact]
  public void IsPathWithinLogRoots_WhenSymlinkInsideRootStaysInside_ReturnsTrue()
  {
    var rootDir = Path.Combine(Path.GetTempPath(), "controlr-logroot-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(rootDir);
    var targetFile = Path.Combine(rootDir, "real.log");
    File.WriteAllText(targetFile, "log");

    try
    {
      var linkPath = Path.Combine(rootDir, "LogFile20260101.log");
      try
      {
        File.CreateSymbolicLink(linkPath, targetFile);
      }
      catch (Exception)
      {
        return;
      }

      Assert.True(CreateManagerWithRoot(rootDir).IsPathWithinLogRoots(linkPath));
    }
    finally
    {
      TryDeleteFile(Path.Combine(rootDir, "LogFile20260101.log"), rootDir);
      TryDeleteFile(targetFile, null);
    }
  }

  [Theory]
  [InlineData("/var/log/controlr-agent/LogFile20260101.log")]
  [InlineData("/var/log/controlr-installer/LogFile20260101.log")]
  [InlineData("/root/.local/share/controlr/logs/LogFile20260101.log")]
  [InlineData("/home/alice/.local/share/controlr/logs/LogFile20260101.log")]
  public void IsPathWithinLogRoots_WhenUnderAKnownLogRoot_ReturnsTrue(string path)
  {
    Assert.True(CreateManager().IsPathWithinLogRoots(path));
  }

  private static FileManager CreateManager()
  {
    var fileSystem = new Mock<IFileSystem>();
    fileSystem.Setup(x => x.GetDirectories(It.Is<string>(p => p == "/home" || p == "/Users")))
      .Returns(["/home/alice"]);
    var aliceHome = new Mock<IFileSystemDirectory>();
    aliceHome.SetupGet(x => x.Name).Returns("alice");
    fileSystem.Setup(x => x.GetDirectoryInfo("/home/alice")).Returns(aliceHome.Object);

    var provider = new Mock<IFileSystemPathProvider>();
    provider.Setup(x => x.GetAgentLogsDirectoryPath()).Returns(AgentLogs);
    provider.Setup(x => x.GetInstallerLogsDirectoryPath()).Returns(InstallerLogs);
    provider.Setup(x => x.GetUnixDesktopClientLogsDirectoryForRoot()).Returns(RootDesktopClientLogs);
    provider.Setup(x => x.GetUnixDesktopClientLogsDirectory(It.IsAny<string>()))
      .Returns((string username) => username == "alice"
        ? AliceDesktopClientLogs
        : $"/home/{username}/.local/share/controlr/logs");

    return new FileManager(fileSystem.Object, provider.Object, NullLogger<FileManager>.Instance);
  }

  private static FileManager CreateManagerWithRoot(string agentLogsRoot)
  {
    var fileSystem = new Mock<IFileSystem>();
    fileSystem.Setup(x => x.GetDirectories(It.IsAny<string>())).Returns(Array.Empty<string>());

    var provider = new Mock<IFileSystemPathProvider>();
    provider.Setup(x => x.GetAgentLogsDirectoryPath()).Returns(agentLogsRoot);
    provider.Setup(x => x.GetInstallerLogsDirectoryPath()).Returns(string.Empty);
    provider.Setup(x => x.GetWindowsDesktopClientLogsDirectory()).Returns(string.Empty);
    provider.Setup(x => x.GetUnixDesktopClientLogsDirectoryForRoot()).Returns(string.Empty);
    provider.Setup(x => x.GetUnixDesktopClientLogsDirectory(It.IsAny<string>())).Returns(string.Empty);

    return new FileManager(fileSystem.Object, provider.Object, NullLogger<FileManager>.Instance);
  }

  private static void TryDeleteFile(string path, string? containingDir)
  {
    try
    {
      if (File.Exists(path) || new FileInfo(path).LinkTarget is not null)
      {
        File.Delete(path);
      }
    }
    catch
    {
    }

    if (containingDir is not null)
    {
      try
      {
        Directory.Delete(containingDir, recursive: true);
      }
      catch
      {
      }
    }
  }
}
