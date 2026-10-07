using ControlR.Agent.Common.Services.FileManager;
using ControlR.Agent.Shared.Services;
using ControlR.Libraries.Api.Contracts.Enums;
using ControlR.Libraries.Shared.Services;
using ControlR.Libraries.TestingUtilities;
using ControlR.Libraries.TestingUtilities.FileSystem;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ControlR.Agent.Common.Tests;

/// <summary>
/// Pins that the log-contents stream is confined to the log roots the agent itself enumerates, so
/// log-read on one device cannot be aimed at an arbitrary file the agent process can open (#260).
/// </summary>
/// <remarks>
/// The root checks run through <see cref="Path.GetFullPath(string)"/>, which always uses the host
/// separator, so the fixtures use host-native paths. Platform selection is mocked, which means the
/// same assertions run on Windows, Linux, and macOS while still exercising each platform's branch.
/// </remarks>
public class FileManagerLogRootsTests
{
  private const string SampleLogFile = "LogFile20260101.log";

  private static string AgentLogs => Join("var", "log", "controlr-agent");
  private static string AliceDesktopClientLogs => Join(AliceHome, ".local", "share", "controlr", "logs");
  private static string AliceHome => Join("home", "alice");
  private static string InstallerLogs => Join("var", "log", "controlr-installer");
  private static string RootDesktopClientLogs => Join("root", ".local", "share", "controlr", "logs");
  private static string WindowsDesktopClientLogs => Join("ProgramData", "ControlR", "Logs");

  [Fact]
  public void IsPathWithinLogRoots_WhenIntermediateDirectoryIsSymlinkLeavingRoot_ReturnsFalse()
  {
    // The final component is an ordinary file, so a check that only resolves the
    // last link would pass this and then follow the directory link on open.
    var fileSystem = CreateLinuxFileSystem();
    var linkedDirectory = Join(AgentLogs, "linked-private");
    fileSystem.AddSymbolicLink(linkedDirectory, Join("etc"));

    Assert.False(CreateLinuxManager(fileSystem).IsPathWithinLogRoots(Join(linkedDirectory, "shadow")));
  }

  [Fact]
  public void IsPathWithinLogRoots_WhenIntermediateDirectoryIsSymlinkStayingInside_ReturnsTrue()
  {
    var fileSystem = CreateLinuxFileSystem();
    var linkedDirectory = Join(AgentLogs, "current");
    fileSystem.AddSymbolicLink(linkedDirectory, Join(AgentLogs));

    Assert.True(CreateLinuxManager(fileSystem).IsPathWithinLogRoots(Join(linkedDirectory, SampleLogFile)));
  }

  [Fact]
  public void IsPathWithinLogRoots_WhenLinkSitsAboveTheLogRoot_ReturnsTrue()
  {
    // macOS ships /var as a link to /private/var, so the path the provider reports
    // and the path the kernel opens differ above the root. The link is the OS's
    // own layout, not a user jump, so the request must still be allowed.
    var fileSystem = CreateLinuxFileSystem();
    fileSystem.AddSymbolicLink(Join("var"), Join("private", "var"));

    var manager = CreateManager(fileSystem, SystemPlatform.MacOs);

    Assert.True(manager.IsPathWithinLogRoots(Join(AgentLogs, SampleLogFile)));
  }

  [Theory]
  [InlineData("etc", "shadow")]
  [InlineData("home", "alice", ".ssh", "id_rsa")]
  [InlineData("var", "log", "controlr-agentevil", "LogFile20260101.log")]
  public void IsPathWithinLogRoots_WhenOutsideTheLogRoots_ReturnsFalse(params string[] segments)
  {
    Assert.False(CreateLinuxManager().IsPathWithinLogRoots(Join(segments ?? [])));
  }

  [Fact]
  public void IsPathWithinLogRoots_WhenPathIsBlank_ReturnsFalse()
  {
    Assert.False(CreateLinuxManager().IsPathWithinLogRoots(string.Empty));
  }

  [Fact]
  public void IsPathWithinLogRoots_WhenPathIsLexicalTraversalOutOfRoot_ReturnsFalse()
  {
    var path = Join(AliceDesktopClientLogs, "..", "..", ".ssh", "id_rsa");

    Assert.False(CreateLinuxManager().IsPathWithinLogRoots(path));
  }

  [Fact]
  public void IsPathWithinLogRoots_WhenPathSitsInSiblingDirectoryWithSharedPrefix_ReturnsFalse()
  {
    Assert.False(CreateLinuxManager().IsPathWithinLogRoots(Join(AgentLogs + "evil", SampleLogFile)));
  }

  [Fact]
  public void IsPathWithinLogRoots_WhenSymlinkInsideRootLeavesTheRoot_ReturnsFalse()
  {
    var fileSystem = CreateLinuxFileSystem();
    var linkPath = Join(AgentLogs, "LogFile20260102.log");
    fileSystem.AddSymbolicLink(linkPath, Join("etc", "shadow"));

    Assert.False(CreateLinuxManager(fileSystem).IsPathWithinLogRoots(linkPath));
  }

  [Fact]
  public void IsPathWithinLogRoots_WhenSymlinkInsideRootStaysInside_ReturnsTrue()
  {
    var fileSystem = CreateLinuxFileSystem();
    var linkPath = Join(AgentLogs, "CurrentLog.log");
    fileSystem.AddSymbolicLink(linkPath, Join(AgentLogs, SampleLogFile));

    Assert.True(CreateLinuxManager(fileSystem).IsPathWithinLogRoots(linkPath));
  }

  [Theory]
  [InlineData("agent")]
  [InlineData("installer")]
  [InlineData("root-desktop")]
  [InlineData("alice-desktop")]
  public void IsPathWithinLogRoots_WhenUnderAKnownLogRoot_ReturnsTrue(string root)
  {
    var path = root switch
    {
      "agent" => Join(AgentLogs, SampleLogFile),
      "installer" => Join(InstallerLogs, SampleLogFile),
      "root-desktop" => Join(RootDesktopClientLogs, SampleLogFile),
      "alice-desktop" => Join(AliceDesktopClientLogs, SampleLogFile),
      _ => throw new ArgumentOutOfRangeException(nameof(root))
    };

    var manager = CreateLinuxManager();
    Assert.True(manager.IsPathWithinLogRoots(path));
  }

  [WindowsOnlyFact]
  public void IsPathWithinLogRoots_WhenWindowsPathDiffersOnlyByCase_ReturnsTrue()
  {
    var fileSystem = new FakeFileSystem('\\');
    fileSystem.AddFile(Join(WindowsDesktopClientLogs, SampleLogFile), "log");

    var manager = CreateManager(fileSystem, SystemPlatform.Windows);

    Assert.True(manager.IsPathWithinLogRoots(Join(WindowsDesktopClientLogs, SampleLogFile).ToUpperInvariant()));
  }

  [Fact]
  public void ResolveLogFile_WhenFileNameContainsSeparator_RejectsSelector()
  {
    var result = CreateLinuxManager().ResolveLogFile(LogKind.Agent, "LogFile/../shadow.log", null);

    Assert.False(result.IsSuccess);
    Assert.Equal(OperationFailureCode.InvalidInput, result.Code);
  }

  [Fact]
  public void ResolveLogFile_WhenFileNameContainsTraversal_RejectsSelector()
  {
    var result = CreateLinuxManager().ResolveLogFile(LogKind.Agent, "../shadow", null);

    Assert.False(result.IsSuccess);
    Assert.Equal(OperationFailureCode.InvalidInput, result.Code);
  }

  [Fact]
  public void ResolveLogFile_WhenKindIsUnknown_RejectsSelector()
  {
    var result = CreateLinuxManager().ResolveLogFile((LogKind)99, SampleLogFile, null);

    Assert.False(result.IsSuccess);
    Assert.Equal(OperationFailureCode.InvalidInput, result.Code);
  }

  [Fact]
  public void ResolveLogFile_WhenSelectingPerUserDesktopLogsOnLinux_UsesThatUsersLogRoot()
  {
    var result = CreateLinuxManager().ResolveLogFile(LogKind.DesktopClient, SampleLogFile, "alice");

    Assert.True(result.IsSuccess);
    Assert.Equal(
      Path.GetFullPath(Join(AliceDesktopClientLogs, SampleLogFile)),
      Path.GetFullPath(result.FileSystemPath));
  }

  [Fact]
  public void ResolveLogFile_WhenSelectingPerUserDesktopLogsOnMacOS_UsesThatUsersLogRoot()
  {
    var fileSystem = new FakeFileSystem('/');
    var userHome = Join("Users", "alice");
    var userLogs = Join(userHome, ".local", "share", "controlr", "logs");
    fileSystem.AddDirectory(Join("Users"));
    fileSystem.AddDirectory(userHome);
    fileSystem.AddFile(Join(userLogs, SampleLogFile), "alice-desktop");

    var result = CreateManager(fileSystem, SystemPlatform.MacOs)
      .ResolveLogFile(LogKind.DesktopClient, SampleLogFile, "alice");

    Assert.True(result.IsSuccess);
    Assert.Equal(
      Path.GetFullPath(Join(userLogs, SampleLogFile)),
      Path.GetFullPath(result.FileSystemPath));
  }

  [Fact]
  public void ResolveLogFile_WhenUsernameContainsSeparator_RejectsSelector()
  {
    var result = CreateLinuxManager().ResolveLogFile(LogKind.DesktopClient, SampleLogFile, "../alice");

    Assert.False(result.IsSuccess);
    Assert.Equal(OperationFailureCode.InvalidInput, result.Code);
  }

  private static Mock<ISystemEnvironment> CreateEnvironment(SystemPlatform platform)
  {
    var environment = new Mock<ISystemEnvironment>();
    environment.Setup(x => x.Platform).Returns(platform);
    environment.Setup(x => x.IsWindows()).Returns(platform == SystemPlatform.Windows);
    environment.Setup(x => x.IsLinux()).Returns(platform == SystemPlatform.Linux);
    environment.Setup(x => x.IsMacOS()).Returns(platform == SystemPlatform.MacOs);
    return environment;
  }

  private static FakeFileSystem CreateLinuxFileSystem()
  {
    var fileSystem = new FakeFileSystem('/');
    fileSystem.AddDirectory(Join("home"));
    fileSystem.AddDirectory(AliceHome);
    fileSystem.AddFile(Join(AgentLogs, SampleLogFile), "agent");
    fileSystem.AddFile(Join(InstallerLogs, SampleLogFile), "installer");
    fileSystem.AddFile(Join(RootDesktopClientLogs, SampleLogFile), "root-desktop");
    fileSystem.AddFile(Join(AliceDesktopClientLogs, SampleLogFile), "alice-desktop");
    return fileSystem;
  }

  private static FileManager CreateLinuxManager(FakeFileSystem? fileSystem = null)
  {
    return CreateManager(fileSystem ?? CreateLinuxFileSystem(), SystemPlatform.Linux);
  }

  private static FileManager CreateManager(FakeFileSystem fileSystem, SystemPlatform platform)
  {
    return CreateManager(fileSystem, CreateEnvironment(platform));
  }

  private static FileManager CreateManager(FakeFileSystem fileSystem, Mock<ISystemEnvironment> environment)
  {
    var provider = new Mock<IFileSystemPathProvider>();
    provider.Setup(x => x.GetAgentLogsDirectoryPath()).Returns(AgentLogs);
    provider.Setup(x => x.GetInstallerLogsDirectoryPath()).Returns(InstallerLogs);
    provider.Setup(x => x.GetWindowsDesktopClientLogsDirectory()).Returns(WindowsDesktopClientLogs);
    provider.Setup(x => x.GetUnixDesktopClientLogsDirectoryForRoot()).Returns(RootDesktopClientLogs);
    provider.Setup(x => x.GetUnixDesktopClientLogsDirectory(It.IsAny<string>()))
      .Returns((string username) => Join(
        environment.Object.IsMacOS() ? "Users" : "home",
        username,
        ".local",
        "share",
        "controlr",
        "logs"));

    return new FileManager(
      fileSystem,
      provider.Object,
      environment.Object,
      NullLogger<FileManager>.Instance);
  }

  private static string Join(params string[]? segments)
  {
    var parts = (segments ?? []).Where(segment => !string.IsNullOrEmpty(segment)).ToArray();
    if (parts.Length == 0)
    {
      return string.Empty;
    }

    var separator = Path.DirectorySeparatorChar;
    var combined = string.Join(separator, parts);
    return combined[0] == separator
      ? combined
      : separator + combined;
  }
}
