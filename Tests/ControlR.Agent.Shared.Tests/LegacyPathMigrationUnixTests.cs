using ControlR.Agent.Shared.Services;
using ControlR.Agent.Shared.Services.Base;
using ControlR.Libraries.TestingUtilities.FileSystem;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ControlR.Agent.Shared.Tests;

public class LegacyPathMigrationUnixTests
{
  [Fact]
  public void RemoveRelocatedInstall_LeavesWhatThisMoveDidNotCarry()
  {
    var fileSystem = new FakeFileSystem('/');
    fileSystem.AddFile("/etc/controlr/instance-1/appsettings.json", "{}");
    fileSystem.AddFile("/etc/controlr/instance-1/settings.json", "{}");
    fileSystem.AddDirectory("/usr/local/bin/ControlR/instance-1");
    var sut = CreateSut(fileSystem, CreatePathProvider(), new Mock<IOptionsAccessor>());

    sut.RemoveRelocatedInstall(CreateRelocation());

    Assert.False(fileSystem.DirectoryExists("/usr/local/bin/ControlR/instance-1"));
    Assert.False(fileSystem.FileExists("/etc/controlr/instance-1/appsettings.json"));

    // The desktop client keeps its own settings in the agent's settings directory. That file belongs to the
    // install that moved rather than to this move, so it stays.
    Assert.True(fileSystem.FileExists("/etc/controlr/instance-1/settings.json"));
  }

  [Fact]
  public void TryStageSettings_WhenTheLegacySettingsFileExists_CopiesItWithTheRunningInstallRestrictions()
  {
    var fileSystem = new FakeFileSystem('/');
    fileSystem.AddFile("/etc/controlr/instance-1/appsettings.json", "{}");
    var optionsAccessor = new Mock<IOptionsAccessor>();
    var sut = CreateSut(fileSystem, CreatePathProvider(), optionsAccessor);

    var result = sut.TryStageSettings(CreateRelocation());

    Assert.True(result.IsSuccess);
    Assert.True(fileSystem.FileExists("/etc/bitbound/controlr/instance-1/appsettings.json"));

    // The copy carries the device signing key in plaintext, and a copy arrives with whatever the host's
    // umask allows rather than with the restrictions the running install put on the original.
    optionsAccessor.Verify(x => x.RestrictAccess("/etc/bitbound/controlr/instance-1/appsettings.json"), Times.Once);
  }

  [Fact]
  public void TryStageSettings_WhenTheLegacySettingsFileIsAbsent_FailsWithoutWritingAnything()
  {
    var fileSystem = new FakeFileSystem('/');
    var sut = CreateSut(fileSystem, CreatePathProvider(), new Mock<IOptionsAccessor>());

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
    Mock<IFileSystemPathProvider> pathProvider,
    Mock<IOptionsAccessor> optionsAccessor)
  {
    return new LegacyPathMigrationUnix(
      fileSystem,
      pathProvider.Object,
      optionsAccessor.Object,
      NullLogger<LegacyPathMigrationUnix>.Instance);
  }
}
