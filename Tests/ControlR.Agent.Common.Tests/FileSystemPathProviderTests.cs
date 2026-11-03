using ControlR.Agent.Shared.Interfaces;
using ControlR.Agent.Shared.Options;
using ControlR.Agent.Shared.Services;
using ControlR.Libraries.Api.Contracts.Enums;
using ControlR.Libraries.Shared.Helpers;
using ControlR.Libraries.Shared.Services;
using ControlR.Libraries.TestingUtilities.FileSystem;
using Moq;

namespace ControlR.Agent.Common.Tests;

public class FileSystemPathProviderTests
{
  private Mock<IElevationChecker> _elevationChecker = null!;
  private FakeFileSystem _fileSystem = null!;
  private FileSystemPathProvider _pathProvider = null!;
  private Mock<ISystemEnvironment> _systemEnvironment = null!;

  [Theory]
  [InlineData(SystemPlatform.Windows, null, false, false, @"C:\ProgramData\Bitbound\ControlR\default\appsettings.json")]
  [InlineData(SystemPlatform.Windows, "localhost", false, false, @"C:\ProgramData\Bitbound\ControlR\localhost\appsettings.json")]
  [InlineData(SystemPlatform.Windows, "controlr.test.com", false, true, @"C:\ProgramData\Bitbound\ControlR\Debug\controlr.test.com\appsettings.json")]
  [InlineData(SystemPlatform.Linux, null, false, false, "/home/testuser/.bitbound/controlr/default/appsettings.json")]
  [InlineData(SystemPlatform.Linux, "localhost", false, false, "/home/testuser/.bitbound/controlr/localhost/appsettings.json")]
  [InlineData(SystemPlatform.Linux, "controlr.test.com", true, false, "/etc/bitbound/controlr/controlr.test.com/appsettings.json")]
  [InlineData(SystemPlatform.MacOs, null, false, false, "/Users/testuser/.bitbound/controlr/default/appsettings.json")]
  [InlineData(SystemPlatform.MacOs, "localhost", false, false, "/Users/testuser/.bitbound/controlr/localhost/appsettings.json")]
  [InlineData(SystemPlatform.MacOs, "controlr.test.com", true, false, "/etc/bitbound/controlr/controlr.test.com/appsettings.json")]
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
  [InlineData(SystemPlatform.Windows, null, @"C:\Program Files\Bitbound\ControlR\default")]
  [InlineData(SystemPlatform.Windows, "localhost", @"C:\Program Files\Bitbound\ControlR\localhost")]
  [InlineData(SystemPlatform.Linux, "localhost", "/usr/local/bin/Bitbound/ControlR/localhost")]
  [InlineData(SystemPlatform.MacOs, "localhost", "/Library/Application Support/Bitbound/ControlR/localhost")]
  public void GetAgentInstallDirectory_PutsTheBrandUnderThePublisher(
    SystemPlatform platform,
    string? instanceId,
    string expectedPath)
  {
    Setup(platform, instanceId);

    var result = _pathProvider.GetAgentInstallDirectory();

    Assert.Equal(expectedPath, result);
  }

  [Theory]
  [InlineData(SystemPlatform.Windows, null, false, false, @"C:\ProgramData\Bitbound\ControlR\default\Logs\ControlR.Agent\LogFile.log")]
  [InlineData(SystemPlatform.Windows, "localhost", false, false, @"C:\ProgramData\Bitbound\ControlR\localhost\Logs\ControlR.Agent\LogFile.log")]
  [InlineData(SystemPlatform.Linux, null, false, false, "/home/testuser/.bitbound/controlr/default/logs/ControlR.Agent/LogFile.log")]
  [InlineData(SystemPlatform.Linux, "controlr.test.com", true, false, "/var/log/bitbound/controlr/controlr.test.com/ControlR.Agent/LogFile.log")]
  [InlineData(SystemPlatform.MacOs, "localhost", false, false, "/Users/testuser/.bitbound/controlr/localhost/logs/ControlR.Agent/LogFile.log")]
  [InlineData(SystemPlatform.MacOs, "controlr.test.com", true, false, "/private/var/log/bitbound/controlr/controlr.test.com/ControlR.Agent/LogFile.log")]
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
  [InlineData(SystemPlatform.Windows, null, false, false, @"C:\ProgramData\Bitbound\ControlR\default\Logs\ControlR.Agent")]
  [InlineData(SystemPlatform.Windows, "localhost", false, true, @"C:\ProgramData\Bitbound\ControlR\Debug\localhost\Logs\ControlR.Agent")]
  [InlineData(SystemPlatform.Linux, null, false, false, "/home/testuser/.bitbound/controlr/default/logs/ControlR.Agent")]
  [InlineData(SystemPlatform.Linux, "controlr.test.com", true, false, "/var/log/bitbound/controlr/controlr.test.com/ControlR.Agent")]
  [InlineData(SystemPlatform.MacOs, "localhost", false, false, "/Users/testuser/.bitbound/controlr/localhost/logs/ControlR.Agent")]
  [InlineData(SystemPlatform.MacOs, "controlr.test.com", true, false, "/private/var/log/bitbound/controlr/controlr.test.com/ControlR.Agent")]
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
  [InlineData(SystemPlatform.Windows, null, false, false, @"C:\ProgramData\Bitbound\ControlR\default\.controlr-bundle.sha256")]
  [InlineData(SystemPlatform.Windows, "localhost", false, false, @"C:\ProgramData\Bitbound\ControlR\localhost\.controlr-bundle.sha256")]
  [InlineData(SystemPlatform.Windows, "controlr.test.com", false, true, @"C:\ProgramData\Bitbound\ControlR\Debug\controlr.test.com\.controlr-bundle.sha256")]
  [InlineData(SystemPlatform.Linux, null, false, false, "/home/testuser/.bitbound/controlr/default/.controlr-bundle.sha256")]
  [InlineData(SystemPlatform.Linux, "localhost", false, false, "/home/testuser/.bitbound/controlr/localhost/.controlr-bundle.sha256")]
  [InlineData(SystemPlatform.Linux, "controlr.test.com", true, false, "/etc/bitbound/controlr/controlr.test.com/.controlr-bundle.sha256")]
  [InlineData(SystemPlatform.MacOs, null, false, false, "/Users/testuser/.bitbound/controlr/default/.controlr-bundle.sha256")]
  [InlineData(SystemPlatform.MacOs, "localhost", false, false, "/Users/testuser/.bitbound/controlr/localhost/.controlr-bundle.sha256")]
  [InlineData(SystemPlatform.MacOs, "controlr.test.com", true, false, "/etc/bitbound/controlr/controlr.test.com/.controlr-bundle.sha256")]
  public void GetBundleHashFilePath_ReturnsSettingsDirectoryPath(
    SystemPlatform platform,
    string? instanceId,
    bool isElevated,
    bool isDebug,
    string expectedPath)
  {
    Setup(platform, instanceId, isElevated, isDebug);

    var result = _pathProvider.GetBundleHashFilePath();

    Assert.Equal(expectedPath, result);
  }

  [Theory]
  [InlineData(null, "/Applications/ControlR.app")]
  [InlineData("controlr.test.com", "/Applications/ControlR.controlr.test.com.app")]
  public void GetBundleRootDirectory_MacOs_ReturnsInstalledAppBundlePath(string? instanceId, string expectedPath)
  {
    Setup(SystemPlatform.MacOs, instanceId);

    var result = _pathProvider.GetBundleRootDirectory();

    Assert.Equal(expectedPath, result);
  }

  [Theory]
  [InlineData(null, "/Applications/ControlR.app/Contents/MacOS/ControlR.DesktopClient")]
  [InlineData("controlr.test.com", "/Applications/ControlR.controlr.test.com.app/Contents/MacOS/ControlR.DesktopClient")]
  public void GetDesktopExecutablePath_MacOs_ReturnsInstalledAppExecutablePath(string? instanceId, string expectedPath)
  {
    Setup(SystemPlatform.MacOs, instanceId);

    var result = _pathProvider.GetDesktopExecutablePath();

    Assert.Equal(expectedPath, result);
  }

  [Theory]
  [InlineData(SystemPlatform.Windows, null, false, false, @"C:\ProgramData\Bitbound\ControlR\default\Logs\ControlR.Agent.Installer\LogFile.log")]
  [InlineData(SystemPlatform.Windows, "localhost", false, false, @"C:\ProgramData\Bitbound\ControlR\localhost\Logs\ControlR.Agent.Installer\LogFile.log")]
  [InlineData(SystemPlatform.Linux, null, false, false, "/home/testuser/.bitbound/controlr/default/logs/ControlR.Agent.Installer/LogFile.log")]
  [InlineData(SystemPlatform.Linux, "controlr.test.com", true, false, "/var/log/bitbound/controlr/controlr.test.com/ControlR.Agent.Installer/LogFile.log")]
  [InlineData(SystemPlatform.MacOs, "localhost", false, false, "/Users/testuser/.bitbound/controlr/localhost/logs/ControlR.Agent.Installer/LogFile.log")]
  [InlineData(SystemPlatform.MacOs, "controlr.test.com", true, false, "/private/var/log/bitbound/controlr/controlr.test.com/ControlR.Agent.Installer/LogFile.log")]
  public void GetInstallerLogFilePath_AppendsLogFileName(
    SystemPlatform platform,
    string? instanceId,
    bool isElevated,
    bool isDebug,
    string expectedPath)
  {
    Setup(platform, instanceId, isElevated, isDebug);

    var result = _pathProvider.GetInstallerLogFilePath();

    Assert.Equal(expectedPath, result);
  }

  [Theory]
  [InlineData(SystemPlatform.Windows, "Acme Corp", "Acme ", null, false, false, @"C:\ProgramData\Acme_Corp\Acme_\default")]
  [InlineData(SystemPlatform.Windows, "Acme Corp", "Acme Remote", null, false, false, @"C:\ProgramData\Acme_Corp\Acme_Remote\default")]
  [InlineData(SystemPlatform.Windows, "Acme Corp", "Acme Remote", "inst-1", false, false, @"C:\ProgramData\Acme_Corp\Acme_Remote\inst-1")]
  [InlineData(SystemPlatform.Windows, "Acme Corp", "Acme Remote", "inst-1", false, true, @"C:\ProgramData\Acme_Corp\Acme_Remote\Debug\inst-1")]
  [InlineData(SystemPlatform.Linux, "Acme Corp", "Acme Remote", "inst-1", true, false, "/etc/acme_corp/acme_remote/inst-1")]
  [InlineData(SystemPlatform.Linux, "Acme Corp", "Acme Remote", "inst-1", false, false, "/home/testuser/.acme_corp/acme_remote/inst-1")]
  [InlineData(SystemPlatform.MacOs, "Acme Corp", "Acme Remote", "inst-1", true, false, "/etc/acme_corp/acme_remote/inst-1")]
  [InlineData(SystemPlatform.MacOs, "Acme Corp", "Acme Remote", "inst-1", false, false, "/Users/testuser/.acme_corp/acme_remote/inst-1")]
  [InlineData(SystemPlatform.Linux, "Acme_Corp", "Acme_Remote", "inst-1", true, false, "/etc/acme_corp/acme_remote/inst-1")]
  public void GetSettingsDirectoryFor_ForeignBrand_ReturnsThatBrandsInstallDirectory(
    SystemPlatform platform,
    string publisherName,
    string brandName,
    string? instanceId,
    bool isElevated,
    bool isDebug,
    string expectedPath)
  {
    Setup(platform, instanceId, isElevated, isDebug);

    var result = _pathProvider.GetSettingsDirectoryFor(publisherName, brandName, instanceId);

    Assert.Equal(expectedPath, result);
  }

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("   ")]
  public void GetSettingsDirectoryFor_MissingBrandName_Throws(string? brandName)
  {
    Setup(SystemPlatform.Windows, null);

    Assert.Throws<ArgumentException>(() =>
      _pathProvider.GetSettingsDirectoryFor("Bitbound", brandName!, "inst-1"));
  }

  [Theory]
  [InlineData(SystemPlatform.Windows, @"C:\ProgramData\Acme_Remote\inst-1")]
  [InlineData(SystemPlatform.Linux, "/etc/acme_remote/inst-1")]
  [InlineData(SystemPlatform.MacOs, "/etc/acme_remote/inst-1")]
  public void GetSettingsDirectoryFor_WithoutAPublisher_ReturnsTheVendorlessPath(
    SystemPlatform platform,
    string expectedPath)
  {
    Setup(platform, "inst-1", isElevated: true);

    // An install from before the publisher became a directory has no vendor level to name, and that is
    // the shape the relocation has to address it by.
    var result = _pathProvider.GetSettingsDirectoryFor(null, "Acme Remote", "inst-1");

    Assert.Equal(expectedPath, result);
  }

  [Theory]
  [InlineData(SystemPlatform.Linux, null, "/var/log/bitbound/controlr/default/ControlR.DesktopClient")]
  [InlineData(SystemPlatform.Linux, "localhost", "/var/log/bitbound/controlr/localhost/ControlR.DesktopClient")]
  [InlineData(SystemPlatform.MacOs, "controlr.test.com", "/private/var/log/bitbound/controlr/controlr.test.com/ControlR.DesktopClient")]
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
  [InlineData(SystemPlatform.Linux, "testuser", null, "/home/testuser/.bitbound/controlr/default/logs/ControlR.DesktopClient")]
  [InlineData(SystemPlatform.Linux, "alice", "localhost", "/home/alice/.bitbound/controlr/localhost/logs/ControlR.DesktopClient")]
  [InlineData(SystemPlatform.MacOs, "bob", "controlr.test.com", "/Users/bob/.bitbound/controlr/controlr.test.com/logs/ControlR.DesktopClient")]
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
  [InlineData(false, null, @"C:\ProgramData\Bitbound\ControlR\default\testuser\DesktopClient\Logs")]
  [InlineData(false, "localhost", @"C:\ProgramData\Bitbound\ControlR\localhost\testuser\DesktopClient\Logs")]
  [InlineData(true, "controlr.test.com", @"C:\ProgramData\Bitbound\ControlR\Debug\controlr.test.com\testuser\DesktopClient\Logs")]
  public void GetWindowsDesktopClientLogsDirectory_ReturnsCorrectStructure(
    bool isDebug,
    string? instanceId,
    string expectedPath)
  {
    Setup(SystemPlatform.Windows, instanceId, false, isDebug);

    var result = _pathProvider.GetWindowsDesktopClientLogsDirectory("testuser");

    Assert.Equal(expectedPath, result);
  }

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("   ")]
  public void GetWindowsDesktopClientLogsDirectory_ThrowsOnEmptyUsername(string? username)
  {
    Setup(SystemPlatform.Windows, null);

    Assert.Throws<ArgumentException>(() =>
      _pathProvider.GetWindowsDesktopClientLogsDirectory(username!));
  }

  [Fact]
  public void GetWindowsDesktopClientLogsDirectory_ThrowsOnNonWindows()
  {
    Setup(SystemPlatform.Linux, null);

    Assert.Throws<PlatformNotSupportedException>(() =>
      _pathProvider.GetWindowsDesktopClientLogsDirectory("testuser"));
  }

  [Theory]
  [InlineData(false, null, @"C:\ProgramData\Bitbound\ControlR\default")]
  [InlineData(false, "localhost", @"C:\ProgramData\Bitbound\ControlR\localhost")]
  [InlineData(true, "controlr.test.com", @"C:\ProgramData\Bitbound\ControlR\Debug\controlr.test.com")]
  public void GetWindowsDesktopClientUsersDirectory_ReturnsCorrectStructure(
    bool isDebug,
    string? instanceId,
    string expectedPath)
  {
    Setup(SystemPlatform.Windows, instanceId, false, isDebug);

    var result = _pathProvider.GetWindowsDesktopClientUsersDirectory();

    Assert.Equal(expectedPath, result);
  }

  [Fact]
  public void GetWindowsDesktopClientUsersDirectory_ThrowsOnNonWindows()
  {
    Setup(SystemPlatform.Linux, null);

    Assert.Throws<PlatformNotSupportedException>(() =>
      _pathProvider.GetWindowsDesktopClientUsersDirectory());
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
        _systemEnvironment.Setup(x => x.GetProgramFilesDirectory())
          .Returns(@"C:\Program Files");
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

    var directorySeparator = platform == SystemPlatform.Windows ? '\\' : '/';
    _fileSystem = new FakeFileSystem(directorySeparator);

    _pathProvider = new FileSystemPathProvider(
      _systemEnvironment.Object,
      _elevationChecker.Object,
      _fileSystem,
      optionsWrapper);
  }
}
