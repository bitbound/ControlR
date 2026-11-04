using System.Runtime.Versioning;
using ControlR.Agent.Shared.Services;
using ControlR.Agent.Shared.Services.Windows;
using ControlR.Libraries.Shared.Constants;
using ControlR.Libraries.Shared.Services.FileSystem;
using ControlR.Libraries.TestingUtilities.FileSystem;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ControlR.Agent.Shared.Tests;

[SupportedOSPlatform("windows8.0")]
public class LegacyPathMigrationWindowsTests
{
  [Fact]
  public void Plan_WhenTheRegisteredDirectoryMatchesNoIdentity_ReportsNothingToMove()
  {
    var pathProvider = CreatePathProvider();
    var sut = CreateSut(pathProvider);

    var relocation = sut.Plan(
      "Acme Corp",
      @"C:\Program Files\Other Vendor\ControlR\instance-1");

    // Moving a directory this build cannot name again would mean removing files it did not write.
    Assert.Null(relocation);
  }

  [Fact]
  public void Plan_WhenTheServiceIsRegisteredAtTheVendorlessPath_NamesThatInstall()
  {
    var pathProvider = CreatePathProvider();
    var sut = CreateSut(pathProvider);

    var relocation = sut.Plan(
      previousPublisher: null,
      registeredDirectory: @"C:\Program Files\ControlR\default");

    Assert.NotNull(relocation);
    Assert.Equal(@"C:\ProgramData\ControlR\default", relocation.SettingsDirectory);
  }

  [Fact]
  public void Plan_WhenTheServiceIsRegisteredUnderThePublisherBeingReplaced_NamesThatInstall()
  {
    var pathProvider = CreatePathProvider();
    var sut = CreateSut(pathProvider);

    var relocation = sut.Plan(
      "Acme Corp",
      @"C:\Program Files\Acme_Corp\ControlR\instance-1");

    // A vendor change moves the same brand between vendor directories. Refusing it would leave the
    // device retrying an install that can never finish.
    Assert.NotNull(relocation);
    Assert.Equal(@"C:\Program Files\Acme_Corp\ControlR\instance-1", relocation.InstallDirectory);
    Assert.Equal(@"C:\ProgramData\Acme_Corp\ControlR\instance-1", relocation.SettingsDirectory);
  }

  [Fact]
  public void RemoveRelocatedInstall_LeavesTheLogHistoryOfTheInstallThatMoved()
  {
    var fileSystem = new FakeFileSystem('\\');
    var oldSettingsDirectory = @"C:\ProgramData\ControlR\default";
    var oldLogFile = $@"{oldSettingsDirectory}\Logs\ControlR.Agent\LogFile.log";
    fileSystem.AddFile($@"{oldSettingsDirectory}\appsettings.json", "{}");
    fileSystem.AddFile(oldLogFile, "history");
    fileSystem.AddDirectory(@"C:\Program Files\ControlR\default");

    var sut = CreateSut(CreatePathProvider(), fileSystem);

    sut.RemoveRelocatedInstall(
      new WindowsPathRelocation(@"C:\Program Files\ControlR\default", oldSettingsDirectory));

    Assert.False(fileSystem.DirectoryExists(@"C:\Program Files\ControlR\default"));
    Assert.False(fileSystem.FileExists($@"{oldSettingsDirectory}\appsettings.json"));

    // The settings directory is also where this brand's log history lives, and it belongs to the install
    // that moved rather than to this move.
    Assert.True(fileSystem.FileExists(oldLogFile));
  }

  private static Mock<IFileSystemPathProvider> CreatePathProvider()
  {
    var pathProvider = new Mock<IFileSystemPathProvider>();

    // The plan asks the path formula for the directory an identity was written into, passing no instance
    // id when the registered path's last segment is the default one, so the mock has to resolve it the
    // same way.
    pathProvider
      .Setup(x => x.GetAgentInstallDirectoryFor("Acme Corp", It.IsAny<string>(), It.IsAny<string?>()))
      .Returns<string, string, string?>(
        (publisher, brand, instanceId) => $@"C:\Program Files\Acme_Corp\{brand}\{Resolve(instanceId)}");
    pathProvider
      .Setup(x => x.GetAgentInstallDirectoryFor(null, It.IsAny<string>(), It.IsAny<string?>()))
      .Returns<string, string, string?>(
        (publisher, brand, instanceId) => $@"C:\Program Files\{brand}\{Resolve(instanceId)}");
    pathProvider
      .Setup(x => x.GetSettingsDirectoryFor("Acme Corp", It.IsAny<string>(), It.IsAny<string?>()))
      .Returns<string, string, string?>(
        (publisher, brand, instanceId) => $@"C:\ProgramData\Acme_Corp\{brand}\{Resolve(instanceId)}");
    pathProvider
      .Setup(x => x.GetSettingsDirectoryFor(null, It.IsAny<string>(), It.IsAny<string?>()))
      .Returns<string, string, string?>(
        (publisher, brand, instanceId) => $@"C:\ProgramData\{brand}\{Resolve(instanceId)}");

    return pathProvider;
  }

  private static LegacyPathMigrationWindows CreateSut(
    Mock<IFileSystemPathProvider> pathProvider,
    IFileSystem? fileSystem = null)
  {
    return new LegacyPathMigrationWindows(
      fileSystem ?? Mock.Of<IFileSystem>(),
      pathProvider.Object,
      Mock.Of<IOptionsAccessor>(),
      NullLogger<LegacyPathMigrationWindows>.Instance);
  }

  private static string Resolve(string? instanceId)
  {
    return instanceId ?? AppConstants.DefaultInstanceId;
  }
}
