using ControlR.Agent.Common.Services.FileManager;
using ControlR.Agent.Shared.Services;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;
using ControlR.Libraries.Api.Contracts.Enums;
using ControlR.Libraries.Shared.Services;
using ControlR.Libraries.TestingUtilities.FileSystem;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ControlR.Agent.Common.Tests;

/// <summary>
/// Pins the per-user Windows desktop-client log tree that RemoteLogs shows. Platform selection is
/// mocked, so these run on any host.
/// </summary>
public class FileManagerWindowsDesktopLogFilesTests
{
  private static string AliceLogs => WindowsDesktopClientLogs("alice");
  private static string BobLogs => WindowsDesktopClientLogs("bob");
  private static string UsersDirectory => Join("ProgramData", "ControlR", "default");

  [Fact]
  public async Task GetLogFiles_OnWindows_GroupsDesktopLogsByUser()
  {
    var fileSystem = new FakeFileSystem();
    fileSystem.AddFile(Join(AliceLogs, "LogFile20260101.log"), "alice log");
    fileSystem.AddFile(Join(BobLogs, "LogFile20260102.log"), "bob log");
    fileSystem.AddFile(Join(BobLogs, "LogFile20260103.log"), "bob log");

    var desktopGroups = GetDesktopGroups(await CreateManager(fileSystem).GetLogFiles());

    Assert.Equal(
      ["DesktopClient Logs (alice)", "DesktopClient Logs (bob)"],
      desktopGroups.Select(x => x.GroupName));
    Assert.Equal(["alice", "bob"], desktopGroups.Select(x => x.Username));
    Assert.Single(desktopGroups[0].LogFiles);
    Assert.Equal(2, desktopGroups[1].LogFiles.Count);
  }

  [Fact]
  public async Task GetLogFiles_OnWindows_WhenNoUserDirectoryExists_ReturnsNoDesktopGroups()
  {
    var desktopGroups = GetDesktopGroups(await CreateManager(new FakeFileSystem()).GetLogFiles());

    Assert.Empty(desktopGroups);
  }

  [Fact]
  public async Task GetLogFiles_OnWindows_WhenUserHasNoLogsDirectory_SkipsThatUser()
  {
    var fileSystem = new FakeFileSystem();
    fileSystem.AddDirectory(Join(UsersDirectory, "carol"));
    fileSystem.AddFile(Join(AliceLogs, "LogFile20260101.log"), "alice log");

    var desktopGroups = GetDesktopGroups(await CreateManager(fileSystem).GetLogFiles());

    Assert.Equal(["alice"], desktopGroups.Select(x => x.Username));
  }

  [Fact]
  public void ResolveLogFile_OnWindows_WhenUsernameProvided_SelectsThatUsersFile()
  {
    var fileSystem = new FakeFileSystem();
    fileSystem.AddFile(Join(BobLogs, "LogFile20260102.log"), "bob log");

    var result = CreateManager(fileSystem).ResolveLogFile(LogKind.DesktopClient, "LogFile20260102.log", "bob");

    Assert.True(result.IsSuccess);
    Assert.Equal(
      Path.GetFullPath(Join(BobLogs, "LogFile20260102.log")),
      Path.GetFullPath(result.FileSystemPath));
  }

  [Fact]
  public void ResolveLogFile_OnWindows_WithoutAUsername_RejectsSelector()
  {
    var fileSystem = new FakeFileSystem();
    fileSystem.AddFile(Join(AliceLogs, "LogFile20260101.log"), "alice log");

    var result = CreateManager(fileSystem).ResolveLogFile(LogKind.DesktopClient, "LogFile20260101.log", null);

    Assert.False(result.IsSuccess);
    Assert.Equal(OperationFailureCode.InvalidInput, result.Code);
  }

  private static FileManager CreateManager(FakeFileSystem fileSystem)
  {
    var environment = new Mock<ISystemEnvironment>();
    environment.Setup(x => x.Platform).Returns(SystemPlatform.Windows);
    environment.Setup(x => x.IsWindows()).Returns(true);

    var provider = new Mock<IFileSystemPathProvider>();
    provider.Setup(x => x.GetAgentLogsDirectoryPath()).Returns(Join("ProgramData", "ControlR", "Agent", "Logs"));
    provider.Setup(x => x.GetInstallerLogsDirectoryPath()).Returns(Join("ProgramData", "ControlR", "Logs"));
    provider.Setup(x => x.GetWindowsDesktopClientUsersDirectory()).Returns(UsersDirectory);
    provider.Setup(x => x.GetWindowsDesktopClientLogsDirectory(It.IsAny<string>()))
      .Returns((string username) => WindowsDesktopClientLogs(username));

    return new FileManager(
      fileSystem,
      provider.Object,
      environment.Object,
      NullLogger<FileManager>.Instance);
  }

  private static List<LogFileGroupDto> GetDesktopGroups(List<LogFileGroupDto> groups)
  {
    return groups.Where(x => x.Kind == LogKind.DesktopClient).ToList();
  }

  private static string Join(params string[] segments)
  {
    var combined = string.Join(Path.DirectorySeparatorChar, segments);

    return combined[0] == Path.DirectorySeparatorChar
      ? combined
      : Path.DirectorySeparatorChar + combined;
  }

  private static string WindowsDesktopClientLogs(string username)
  {
    return Join("ProgramData", "ControlR", "default", username, "DesktopClient", "Logs");
  }
}
