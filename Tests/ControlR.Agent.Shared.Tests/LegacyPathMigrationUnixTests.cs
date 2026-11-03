using ControlR.Agent.Shared.Services;
using ControlR.Agent.Shared.Services.Base;
using ControlR.Libraries.TestingUtilities.FileSystem;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ControlR.Agent.Shared.Tests;

public class LegacyPathMigrationUnixTests
{
  [Fact]
  public void TryStageSettings_WhenTheLegacySettingsFileExists_CopiesItIntoTheNewDirectory()
  {
    var fileSystem = new FakeFileSystem('/');
    fileSystem.AddFile("/etc/controlr/instance-1/appsettings.json", "{}");
    var sut = CreateSut(fileSystem, CreatePathProvider());

    var result = sut.TryStageSettings(CreateRelocation());

    Assert.True(result.IsSuccess);
    Assert.True(fileSystem.FileExists("/etc/bitbound/controlr/instance-1/appsettings.json"));
  }

  [Fact]
  public void TryStageSettings_WhenTheLegacySettingsFileIsAbsent_FailsWithoutWritingAnything()
  {
    var fileSystem = new FakeFileSystem('/');
    var sut = CreateSut(fileSystem, CreatePathProvider());

    var result = sut.TryStageSettings(CreateRelocation());

    // Without the signing key the relocated install would register a new device, and the old record
    // could never authenticate again, so the move is refused rather than attempted.
    Assert.False(result.IsSuccess);
    Assert.False(fileSystem.DirectoryExists("/etc/bitbound/controlr/instance-1"));
  }

  private static Mock<IFileSystemPathProvider> CreatePathProvider()
  {
    var pathProvider = new Mock<IFileSystemPathProvider>();
    pathProvider
      .Setup(x => x.GetEffectiveInstanceId())
      .Returns("instance-1");
    pathProvider
      .Setup(x => x.GetAgentInstallDirectoryFor(null, It.IsAny<string>(), It.IsAny<string?>()))
      .Returns("/usr/local/bin/ControlR/instance-1");
    pathProvider
      .Setup(x => x.GetSettingsDirectoryFor(null, It.IsAny<string>(), It.IsAny<string?>()))
      .Returns("/etc/controlr/instance-1");
    pathProvider
      .Setup(x => x.GetSettingsDirectoryFor("Bitbound", It.IsAny<string>(), It.IsAny<string?>()))
      .Returns("/etc/bitbound/controlr/instance-1");
    return pathProvider;
  }

  private static UnixPathRelocation CreateRelocation()
  {
    return new UnixPathRelocation(
      "/usr/local/bin/ControlR/instance-1",
      "/etc/controlr/instance-1");
  }

  private static LegacyPathMigrationUnix CreateSut(
    FakeFileSystem fileSystem,
    Mock<IFileSystemPathProvider> pathProvider)
  {
    return new LegacyPathMigrationUnix(
      fileSystem,
      pathProvider.Object,
      NullLogger<LegacyPathMigrationUnix>.Instance);
  }
}
