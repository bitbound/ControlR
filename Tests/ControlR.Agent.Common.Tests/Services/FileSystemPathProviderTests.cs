using ControlR.Agent.Common.Interfaces;
using ControlR.Agent.Common.Options;
using ControlR.Agent.Common.Services;
using ControlR.Libraries.DevicesCommon.Services;
using ControlR.Libraries.Shared.Enums;
using ControlR.Libraries.Shared.Helpers;
using ControlR.Libraries.Shared.Services;
using ControlR.Tests.TestingUtilities;
using Moq;
using Xunit.Abstractions;

namespace ControlR.Agent.Common.Tests.Services;

public class FileSystemPathProviderTests(ITestOutputHelper testOutputHelper)
{
  private readonly ITestOutputHelper _testOutputHelper = testOutputHelper;

  private Mock<IElevationChecker> _elevationChecker = null!;
  private Mock<IFileSystem> _fileSystem = null!;
  private FileSystemPathProvider _pathProvider = null!;
  private Mock<ISystemEnvironment> _systemEnvironment = null!;

  [Theory]
  [InlineData(SystemPlatform.Windows, null, false, false, @"C:\ProgramData\ControlR\appsettings.json")]
  [InlineData(SystemPlatform.Windows, "localhost", false, false, @"C:\ProgramData\ControlR\localhost\appsettings.json")]
  [InlineData(SystemPlatform.Windows, "controlr.test.com", false, true, @"C:\ProgramData\ControlR\Debug\controlr.test.com\appsettings.json")]
  [InlineData(SystemPlatform.Linux, null, false, false, "/home/testuser/.controlr/appsettings.json")]
  [InlineData(SystemPlatform.Linux, "localhost", false, false, "/home/testuser/.controlr/localhost/appsettings.json")]
  [InlineData(SystemPlatform.Linux, "controlr.test.com", true, false, "/etc/controlr/controlr.test.com/appsettings.json")]
  [InlineData(SystemPlatform.MacOs, null, false, false, "/Users/testuser/.controlr/appsettings.json")]
  [InlineData(SystemPlatform.MacOs, "localhost", false, false, "/Users/testuser/.controlr/localhost/appsettings.json")]
  [InlineData(SystemPlatform.MacOs, "controlr.test.com", true, false, "/etc/controlr/controlr.test.com/appsettings.json")]
  public void GetAgentAppSettingsPath_ReturnsCorrectPath(
    SystemPlatform platform,
    string? instanceId,
    bool isElevated,
    bool isDebug,
    string expectedPath)
  {
    Setup(platform, instanceId, isElevated, isDebug);

    var result = _pathProvider.GetAgentAppSettingsPath();
    Assert.Equal(expectedPath, result);
    Assert.EndsWith("appsettings.json", result);
  }

  [Theory]
  [InlineData(SystemPlatform.Windows, null, false, false, @"C:\ProgramData\ControlR\Logs\ControlR.Agent\LogFile.log")]
  [InlineData(SystemPlatform.Windows, "localhost", false, false, @"C:\ProgramData\ControlR\localhost\Logs\ControlR.Agent\LogFile.log")]
  [InlineData(SystemPlatform.Linux, null, false, false, "/home/testuser/.controlr/logs/ControlR.Agent/LogFile.log")]
  [InlineData(SystemPlatform.Linux, "controlr.test.com", true, false, "/var/log/controlr/controlr.test.com/ControlR.Agent/LogFile.log")]
  [InlineData(SystemPlatform.MacOs, "localhost", false, false, "/Users/testuser/.controlr/localhost/logs/ControlR.Agent/LogFile.log")]
  public void GetAgentLogFilePath_AppendsLogFileName(
    SystemPlatform platform,
    string? instanceId,
    bool isElevated,
    bool isDebug,
    string expectedPath)
  {
    Setup(platform, instanceId, isElevated, isDebug);

    var result = _pathProvider.GetAgentLogFilePath();

    Assert.Equal(expectedPath, result);
  }

  [Theory]
  [InlineData(SystemPlatform.Windows, null, false, false, @"C:\ProgramData\ControlR\Logs\ControlR.Agent")]
  [InlineData(SystemPlatform.Windows, "localhost", false, true, @"C:\ProgramData\ControlR\Debug\localhost\Logs\ControlR.Agent")]
  [InlineData(SystemPlatform.Linux, null, false, false, "/home/testuser/.controlr/logs/ControlR.Agent")]
  [InlineData(SystemPlatform.Linux, "controlr.test.com", true, false, "/var/log/controlr/controlr.test.com/ControlR.Agent")]
  [InlineData(SystemPlatform.MacOs, "localhost", false, false, "/Users/testuser/.controlr/localhost/logs/ControlR.Agent")]
  public void GetAgentLogsDirectoryPath_ReturnsCorrectStructure(
    SystemPlatform platform,
    string? instanceId,
    bool isElevated,
    bool isDebug,
    string expectedPath)
  {
    Setup(platform, instanceId, isElevated, isDebug);

    var result = _pathProvider.GetAgentLogsDirectoryPath();

    Assert.Equal(expectedPath, result);
  }

  [Theory]
  [InlineData(SystemPlatform.Linux, null, "/var/log/controlr/ControlR.DesktopClient")]
  [InlineData(SystemPlatform.Linux, "localhost", "/var/log/controlr/localhost/ControlR.DesktopClient")]
  [InlineData(SystemPlatform.MacOs, "controlr.test.com", "/var/log/controlr/controlr.test.com/ControlR.DesktopClient")]
  public void GetUnixDesktopClientLogsDirectoryForRoot_ReturnsCorrectStructure(
    SystemPlatform platform,
    string? instanceId,
    string expectedPath)
  {
    Setup(platform, instanceId);

    var result = _pathProvider.GetUnixDesktopClientLogsDirectoryForRoot();

    Assert.Equal(expectedPath, result);
  }

  [Theory]
  [InlineData(SystemPlatform.Linux, "testuser", null, "/home/testuser/.controlr/logs/ControlR.DesktopClient")]
  [InlineData(SystemPlatform.Linux, "alice", "localhost", "/home/alice/.controlr/localhost/logs/ControlR.DesktopClient")]
  [InlineData(SystemPlatform.MacOs, "bob", "controlr.test.com", "/Users/bob/.controlr/controlr.test.com/logs/ControlR.DesktopClient")]
  public void GetUnixDesktopClientLogsDirectory_ReturnsCorrectStructure(
    SystemPlatform platform,
    string username,
    string? instanceId,
    string expectedPath)
  {
    Setup(platform, instanceId);

    var result = _pathProvider.GetUnixDesktopClientLogsDirectory(username);

    Assert.Equal(expectedPath, result);
  }

  [Theory]
  [InlineData(SystemPlatform.Linux, null)]
  [InlineData(SystemPlatform.MacOs, "")]
  public void GetUnixDesktopClientLogsDirectory_ThrowsOnEmptyUsername(SystemPlatform platform, string? username)
  {
    Setup(platform, null);

    Assert.Throws<ArgumentException>(() =>
      _pathProvider.GetUnixDesktopClientLogsDirectory(username!));
  }

  [Fact]
  public void GetUnixDesktopClientLogsDirectory_ThrowsOnWindows()
  {
    Setup(SystemPlatform.Windows, null);

    Assert.Throws<PlatformNotSupportedException>(() =>
      _pathProvider.GetUnixDesktopClientLogsDirectory("testuser"));
  }

  [Theory]
  [InlineData(false, null, @"C:\ProgramData\ControlR\Logs\ControlR.DesktopClient")]
  [InlineData(false, "localhost", @"C:\ProgramData\ControlR\localhost\Logs\ControlR.DesktopClient")]
  [InlineData(true, "controlr.test.com", @"C:\ProgramData\ControlR\Debug\controlr.test.com\Logs\ControlR.DesktopClient")]
  public void GetWindowsDesktopClientLogsDirectory_ReturnsCorrectStructure(
    bool isDebug,
    string? instanceId,
    string expectedPath)
  {
    Setup(SystemPlatform.Windows, instanceId, false, isDebug);

    var result = _pathProvider.GetWindowsDesktopClientLogsDirectory();

    Assert.Equal(expectedPath, result);
  }

  [Fact]
  public void GetWindowsDesktopClientLogsDirectory_ThrowsOnNonWindows()
  {
    Setup(SystemPlatform.Linux, null);

    Assert.Throws<PlatformNotSupportedException>(() =>
      _pathProvider.GetWindowsDesktopClientLogsDirectory());
  }

  private void Setup(SystemPlatform platform, string? instanceId, bool isElevated = false, bool isDebug = false)
  {
    _systemEnvironment = new Mock<ISystemEnvironment>();
    _systemEnvironment.Setup(x => x.Platform).Returns(platform);
    _systemEnvironment.Setup(x => x.IsDebug).Returns(isDebug);
    _systemEnvironment.Setup(x => x.IsWindows()).Returns(platform == SystemPlatform.Windows);
    _systemEnvironment.Setup(x => x.IsLinux()).Returns(platform == SystemPlatform.Linux);
    _systemEnvironment.Setup(x => x.IsMacOS()).Returns(platform == SystemPlatform.MacOs);

    switch (platform)
    {
      case SystemPlatform.Windows:
        _systemEnvironment
          .Setup(x => x.GetProfileDirectory())
          .Returns(@"C:\Users\TestUser");

        _systemEnvironment.Setup(x => x.GetCommonApplicationDataDirectory())
          .Returns(@"C:\ProgramData");
        break;
      case SystemPlatform.Linux:
        if (isElevated)
        {
          _systemEnvironment
            .Setup(x => x.GetProfileDirectory())
            .Returns("/root");
        }
        else
        {
          _systemEnvironment
            .Setup(x => x.GetProfileDirectory())
            .Returns("/home/testuser");
        }
        break;
      case SystemPlatform.MacOs:
        if (isElevated)
        {
          _systemEnvironment
            .Setup(x => x.GetProfileDirectory())
            .Returns("/var/root");
        }
        else
        {
          _systemEnvironment
            .Setup(x => x.GetProfileDirectory())
            .Returns("/Users/testuser");
        }
        break;
    }

    var instanceOptions = new InstanceOptions { InstanceId = instanceId };
    var optionsWrapper = new OptionsMonitorWrapper<InstanceOptions>(instanceOptions);

    _elevationChecker = new Mock<IElevationChecker>();
    _elevationChecker.Setup(x => x.IsElevated()).Returns(isElevated);
    
    var fileSystemImpl = new FileSystem(new XunitLogger<FileSystem>(_testOutputHelper));
    _fileSystem = new Mock<IFileSystem>();
    _fileSystem
      .Setup(x => x.CreateDirectory(It.IsAny<string>()))
      .Returns((string path) => new DirectoryInfo(path));
    _fileSystem
      .Setup(x => x.JoinPaths(It.IsAny<char>(), It.IsAny<string[]>()))
      .Returns((char separator, string[] paths) => fileSystemImpl.JoinPaths(separator, paths));
    
    _pathProvider = new FileSystemPathProvider(
      _systemEnvironment.Object,
      _elevationChecker.Object,
      _fileSystem.Object,
      optionsWrapper);
  }
}
