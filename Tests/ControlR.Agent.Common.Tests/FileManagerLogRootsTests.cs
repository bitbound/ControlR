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
}
