using System.Runtime.Versioning;
using ControlR.Agent.Shared.Interfaces;
using ControlR.Agent.Shared.Models;
using ControlR.Agent.Shared.Options;
using ControlR.Agent.Shared.Services;
using ControlR.Agent.Shared.Services.Windows;
using ControlR.ApiClient;
using ControlR.Libraries.Api.Contracts.Enums;
using ControlR.Libraries.Shared.Constants;
using ControlR.Libraries.Shared.Services;
using ControlR.Libraries.Shared.Services.Encryption;
using ControlR.Libraries.Shared.Services.FileSystem;
using ControlR.Libraries.Shared.Services.Processes;
using ControlR.Libraries.TestingUtilities;
using ControlR.Libraries.TestingUtilities.FileSystem;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace ControlR.Agent.Shared.Tests;

[SupportedOSPlatform("windows8.0")]
public class AgentInstallerWindowsRepairTests
{
  [WindowsOnlyFact]
  public async Task RepairDesktopClient_WaitsForExitedProcessBeforeReplacingDirectory()
  {
    var bundleZipPath = @"C:\temp\bundle.zip";
    var installDir = Path.Combine(Path.GetTempPath(), "ControlR", "Install", AppConstants.DefaultInstanceId);
    var desktopClientPath = Path.Combine(installDir, "DesktopClient", "ControlR.DesktopClient.exe");
    var fileSystem = new FakeFileSystem('\\');
    var process = new Mock<IProcess>();
    var processManager = new Mock<IProcessManager>();
    var retryer = new Mock<IRetryer>();
    var pathProvider = new Mock<IFileSystemPathProvider>();
    var systemEnvironment = new Mock<ISystemEnvironment>();
    var sequence = new MockSequence();

    fileSystem.AddDirectory(installDir);
    fileSystem.AddDirectory(Path.Combine(installDir, "DesktopClient"));
    fileSystem.AddFile(desktopClientPath, []);
    fileSystem.AddDirectory(Path.Combine(installDir, "DesktopClient.backup-000000000000000000000000000000000"));
    fileSystem.AddDirectory(@"C:\temp\.controlr-desktop-repair-1234567890abcdef");
    fileSystem.AddDirectory(@"C:\temp\.controlr-desktop-repair-1234567890abcdef\DesktopClient");
    fileSystem.AddFile(Path.Combine(@"C:\temp\.controlr-desktop-repair-1234567890abcdef\DesktopClient", "ControlR.DesktopClient.exe"), new byte[0]);

    systemEnvironment.SetupGet(x => x.IsDebug).Returns(true);
    systemEnvironment.SetupGet(x => x.StartupDirectory).Returns(@"C:\somewhere-else");
    systemEnvironment.SetupGet(x => x.Platform).Returns(SystemPlatform.Windows);

    process.SetupGet(x => x.FilePath).Returns(desktopClientPath);
    process.SetupGet(x => x.Id).Returns(42);
    process.InSequence(sequence).Setup(x => x.Kill());
    process
      .InSequence(sequence)
      .Setup(x => x.WaitForExitAsync(It.IsAny<CancellationToken>()))
      .Returns(Task.CompletedTask);

    processManager
      .Setup(x => x.GetProcessesByName("ControlR.DesktopClient"))
      .Returns([process.Object]);

    retryer
      .Setup(x => x.Retry(It.IsAny<Func<Task>>(), It.IsAny<int>(), It.IsAny<TimeSpan>()))
      .Returns<Func<Task>, int, TimeSpan>((func, _, _) => func());

    pathProvider
      .Setup(x => x.GetAgentInstallDirectory())
      .Returns(installDir);
    pathProvider
      .Setup(x => x.GetBundleHashFilePath())
      .Returns(@"C:\ProgramData\ControlR\bundle.hash");

    var sut = CreateSut(fileSystem, processManager, retryer, pathProvider, systemEnvironment);

    await sut.RepairDesktopClient(CreateRequest(bundleZipPath));

    process.Verify(x => x.Kill(), Times.Once);
    process.Verify(x => x.WaitForExitAsync(It.IsAny<CancellationToken>()), Times.Once);
  }

  [WindowsOnlyFact]
  public async Task RepairDesktopClient_WhenProcessDoesNotExit_DoesNotReplaceDirectory()
  {
    var bundleZipPath = @"C:\temp\bundle.zip";
    var installDir = Path.Combine(Path.GetTempPath(), "ControlR", "Install", AppConstants.DefaultInstanceId);
    var desktopClientPath = Path.Combine(installDir, "DesktopClient", "ControlR.DesktopClient.exe");
    var fileSystem = new FakeFileSystem('\\');
    var process = new Mock<IProcess>();
    var processManager = new Mock<IProcessManager>();
    var retryer = new Mock<IRetryer>();
    var pathProvider = new Mock<IFileSystemPathProvider>();
    var systemEnvironment = new Mock<ISystemEnvironment>();
    var sequence = new MockSequence();

    fileSystem.AddDirectory(installDir);
    fileSystem.AddDirectory(Path.Combine(installDir, "DesktopClient"));
    fileSystem.AddFile(desktopClientPath, []);
    fileSystem.AddDirectory(Path.Combine(installDir, "DesktopClient.backup-000000000000000000000000000000000"));
    fileSystem.AddDirectory(Path.Combine(Path.GetTempPath(), ".controlr-desktop-repair-1234567890abcdef"));
    fileSystem.AddDirectory(Path.Combine(Path.GetTempPath(), ".controlr-desktop-repair-1234567890abcdef", "DesktopClient"));
    fileSystem.AddFile(Path.Combine(Path.GetTempPath(), ".controlr-desktop-repair-1234567890abcdef", "DesktopClient", "ControlR.DesktopClient.exe"), new byte[0]);

    systemEnvironment.SetupGet(x => x.IsDebug).Returns(true);
    systemEnvironment.SetupGet(x => x.StartupDirectory).Returns(@"C:\somewhere-else");
    systemEnvironment.SetupGet(x => x.Platform).Returns(SystemPlatform.Windows);

    process.SetupGet(x => x.FilePath).Returns(desktopClientPath);
    process.SetupGet(x => x.Id).Returns(42);
    process.InSequence(sequence).Setup(x => x.Kill());
    process
      .InSequence(sequence)
      .Setup(x => x.WaitForExitAsync(It.IsAny<CancellationToken>()))
      .Returns(Task.FromCanceled(new CancellationToken(canceled: true)));

    processManager
      .Setup(x => x.GetProcessesByName("ControlR.DesktopClient"))
      .Returns([process.Object]);

    retryer
      .Setup(x => x.Retry(It.IsAny<Func<Task>>(), It.IsAny<int>(), It.IsAny<TimeSpan>()))
      .Returns<Func<Task>, int, TimeSpan>((func, _, _) => func());

    pathProvider
      .Setup(x => x.GetAgentInstallDirectory())
      .Returns(installDir);
    pathProvider
      .Setup(x => x.GetBundleHashFilePath())
      .Returns(@"C:\ProgramData\ControlR\bundle.hash");

    var sut = CreateSut(fileSystem, processManager, retryer, pathProvider, systemEnvironment);

    await sut.RepairDesktopClient(CreateRequest(bundleZipPath));

    Assert.True(fileSystem.DirectoryExists(Path.Combine(installDir, "DesktopClient")));
  }

  private static AgentInstallRequest CreateRequest(string bundleZipPath)
  {
    return new AgentInstallRequest
    {
      BundleZipPath = bundleZipPath,
      BundleSha256 = "hash",
      DeviceId = Guid.NewGuid(),
      ServerUri = new Uri("https://example.test"),
      TenantId = Guid.NewGuid()
    };
  }

  private static AgentInstallerWindows CreateSut(
    IFileSystem fileSystem,
    Mock<IProcessManager> processManager,
    Mock<IRetryer> retryer,
    Mock<IFileSystemPathProvider> pathProvider,
    Mock<ISystemEnvironment> systemEnvironment)
  {
    return new AgentInstallerWindows(
      Mock.Of<IHostApplicationLifetime>(),
      processManager.Object,
      systemEnvironment.Object,
      Mock.Of<IElevationChecker>(),
      retryer.Object,
      Mock.Of<IControlrApi>(),
      Mock.Of<IDeviceInfoProvider>(),
      pathProvider.Object,
      Mock.Of<IRegistryAccessor>(),
      Microsoft.Extensions.Options.Options.Create(new InstanceOptions()),
      fileSystem,
      Mock.Of<IOptionsAccessor>(),
      Mock.Of<IOptionsMonitor<AgentAppOptions>>(),
      Mock.Of<IEd25519KeyProvider>(),
      NullLogger<AgentInstallerWindows>.Instance);
  }
}