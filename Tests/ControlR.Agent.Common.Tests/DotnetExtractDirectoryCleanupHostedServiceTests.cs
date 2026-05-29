using ControlR.Agent.Common.Services;
using ControlR.Agent.Shared.Interfaces;
using ControlR.Agent.Shared.Services;
using ControlR.Libraries.Api.Contracts.Enums;
using ControlR.Libraries.Shared.Services;
using ControlR.Libraries.Shared.Services.FileSystem;
using ControlR.Libraries.Shared.Services.Processes;
using ControlR.Libraries.TestingUtilities.FileSystem;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ControlR.Agent.Common.Tests;

public class DotnetExtractDirectoryCleanupHostedServiceTests
{
  private const string WindowsExtractDirectory = @"C:\Windows\SystemTemp\.net";

  [Fact]
  public async Task StartAsync_Elevated_ContinuesWhenDirectoryDeleteFails()
  {
    var newest = $@"{WindowsExtractDirectory}\newest";
    var second = $@"{WindowsExtractDirectory}\second";
    var oldest = $@"{WindowsExtractDirectory}\oldest";
    var now = DateTime.UtcNow;

    var fileSystem = new Mock<IFileSystem>(MockBehavior.Strict);
    fileSystem.Setup(x => x.DirectoryExists(WindowsExtractDirectory)).Returns(true);
    fileSystem.Setup(x => x.GetDirectories(WindowsExtractDirectory)).Returns([oldest, second, newest]);
    fileSystem.Setup(x => x.GetDirectoryInfo(newest)).Returns(new FakeFileSystemDirectoryInfo(newest) { CreationTime = now.AddMinutes(-1) });
    fileSystem.Setup(x => x.GetDirectoryInfo(second)).Returns(new FakeFileSystemDirectoryInfo(second) { CreationTime = now.AddMinutes(-2) });
    fileSystem.Setup(x => x.GetDirectoryInfo(oldest)).Returns(new FakeFileSystemDirectoryInfo(oldest) { CreationTime = now.AddMinutes(-3) });
    fileSystem.Setup(x => x.DeleteDirectory(second, true)).Throws(new IOException("locked"));
    fileSystem.Setup(x => x.DeleteDirectory(oldest, true));

    var processManager = new Mock<IProcessManager>(MockBehavior.Strict);
    processManager.Setup(x => x.GetProcessesByName("ControlR.Agent")).Returns([]);

    var service = CreateService(
      fileSystem.Object,
      processManager.Object,
      CreateElevationChecker(true).Object,
      CreateSystemEnvironment(SystemPlatform.Windows).Object);

    await service.StartAsync(CancellationToken.None);

    fileSystem.Verify(x => x.DeleteDirectory(second, true), Times.Once);
    fileSystem.Verify(x => x.DeleteDirectory(oldest, true), Times.Once);
    fileSystem.Verify(x => x.DeleteDirectory(newest, true), Times.Never);
  }

  [Fact]
  public async Task StartAsync_Elevated_DeletesOldestSubdirectoriesUsingFakeFileSystem()
  {
    var fileSystem = new FakeFileSystem('\\');
    var now = DateTime.UtcNow;
    fileSystem.AddDirectory(WindowsExtractDirectory);
    fileSystem.AddDirectory($@"{WindowsExtractDirectory}\oldest", creationTime: now.AddMinutes(-3), lastWriteTime: now.AddMinutes(-3));
    fileSystem.AddDirectory($@"{WindowsExtractDirectory}\second", creationTime: now.AddMinutes(-2), lastWriteTime: now.AddMinutes(-2));
    fileSystem.AddDirectory($@"{WindowsExtractDirectory}\newest", creationTime: now.AddMinutes(-1), lastWriteTime: now.AddMinutes(-1));

    var processManager = new Mock<IProcessManager>(MockBehavior.Strict);
    processManager
      .Setup(x => x.GetProcessesByName("ControlR.Agent"))
      .Returns([Mock.Of<IProcess>()]);

    var service = CreateService(
      fileSystem,
      processManager.Object,
      CreateElevationChecker(true).Object,
      CreateSystemEnvironment(SystemPlatform.Windows).Object);

    await service.StartAsync(CancellationToken.None);

    Assert.False(fileSystem.DirectoryExists($@"{WindowsExtractDirectory}\oldest"));
    Assert.True(fileSystem.DirectoryExists($@"{WindowsExtractDirectory}\second"));
    Assert.True(fileSystem.DirectoryExists($@"{WindowsExtractDirectory}\newest"));
  }

  [Fact]
  public async Task StartAsync_NotElevated_DoesNotAccessProcessManager()
  {
    var fileSystem = new FakeFileSystem('\\');
    var processManager = new Mock<IProcessManager>(MockBehavior.Strict);
    var service = CreateService(
      fileSystem,
      processManager.Object,
      CreateElevationChecker(false).Object,
      CreateSystemEnvironment(SystemPlatform.Windows).Object);

    await service.StartAsync(CancellationToken.None);

    processManager.Verify(x => x.GetProcessesByName(It.IsAny<string>()), Times.Never);
  }

  [Fact]
  public async Task StartAsync_UnknownPlatform_DoesNotTryCleanup()
  {
    var fileSystem = new Mock<IFileSystem>(MockBehavior.Strict);
    var processManager = new Mock<IProcessManager>(MockBehavior.Strict);
    var service = CreateService(
      fileSystem.Object,
      processManager.Object,
      CreateElevationChecker(true).Object,
      CreateSystemEnvironment(SystemPlatform.Unknown).Object);

    await service.StartAsync(CancellationToken.None);

    processManager.Verify(x => x.GetProcessesByName(It.IsAny<string>()), Times.Never);
  }

  private static Mock<IElevationChecker> CreateElevationChecker(bool isElevated)
  {
    var elevationChecker = new Mock<IElevationChecker>(MockBehavior.Strict);
    elevationChecker.Setup(x => x.IsElevated()).Returns(isElevated);
    return elevationChecker;
  }

  private static DotnetExtractDirectoryCleanupHostedService CreateService(
    IFileSystem fileSystem,
    IProcessManager processManager,
    IElevationChecker elevationChecker,
    ISystemEnvironment systemEnvironment)
  {
    var pathProvider = new Mock<IFileSystemPathProvider>(MockBehavior.Strict);
    pathProvider.Setup(x => x.GetDotnetExtractDirectory()).Returns(WindowsExtractDirectory);

    return new DotnetExtractDirectoryCleanupHostedService(
      fileSystem,
      processManager,
      elevationChecker,
      systemEnvironment,
      pathProvider.Object,
      NullLogger<DotnetExtractDirectoryCleanupHostedService>.Instance);
  }

  private static Mock<ISystemEnvironment> CreateSystemEnvironment(SystemPlatform platform)
  {
    var systemEnvironment = new Mock<ISystemEnvironment>(MockBehavior.Strict);
    systemEnvironment.Setup(x => x.Platform).Returns(platform);
    return systemEnvironment;
  }
}
