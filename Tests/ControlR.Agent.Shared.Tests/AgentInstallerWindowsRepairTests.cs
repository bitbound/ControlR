using System.Runtime.Versioning;
using ControlR.Agent.Shared.Interfaces;
using ControlR.Agent.Shared.Models;
using ControlR.Agent.Shared.Options;
using ControlR.Agent.Shared.Services;
using ControlR.Agent.Shared.Services.Windows;
using ControlR.ApiClient;
using ControlR.Libraries.Api.Contracts.Enums;
using ControlR.Libraries.Shared.Constants;
using ControlR.Libraries.Shared.Primitives;
using ControlR.Libraries.Shared.Services;
using ControlR.Libraries.Shared.Services.Encryption;
using ControlR.Libraries.Shared.Services.FileSystem;
using ControlR.Libraries.Shared.Services.Processes;
using ControlR.Libraries.TestingUtilities;
using ControlR.Libraries.TestingUtilities.FileSystem;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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

  [WindowsOnlyFact]
  public async Task RestorePreviousBrand_RunsItsStartServiceCommand()
  {
    var previousBrand = "Acme Remote";
    var previousInstallDirectory = @"C:\Program Files\Acme_Remote\instance-1";
    // The replaced install's executable is named for its own brand, not for this build's.
    var previousAgentPath = Path.Combine(previousInstallDirectory, "Acme_Remote.Agent.exe");
    var fileSystem = new FakeFileSystem('\\');
    var processManager = new Mock<IProcessManager>();
    var pathProvider = new Mock<IFileSystemPathProvider>();
    var systemEnvironment = new Mock<ISystemEnvironment>();
    var stagedCommand = string.Empty;

    fileSystem.AddFile(previousAgentPath, []);
    fileSystem.AddDirectory(Path.GetTempPath());

    systemEnvironment.SetupGet(x => x.Platform).Returns(SystemPlatform.Windows);
    pathProvider
      .Setup(x => x.GetAgentInstallDirectoryFor(previousBrand, It.IsAny<string?>()))
      .Returns(previousInstallDirectory);

    processManager
      .Setup(x => x.StartAndWaitForExit(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<TimeSpan>()))
      .Returns<string, string, bool, TimeSpan>((fileName, arguments, _, _) =>
      {
        stagedCommand = arguments;
        fileSystem.AddFile(fileName, []);
        return Task.FromResult(0);
      });

    var sut = CreateSut(fileSystem, processManager, new Mock<IRetryer>(), pathProvider, systemEnvironment);

    var result = await sut.RestorePreviousBrand(previousBrand);

    Assert.True(result.IsSuccess);
    Assert.Equal("start-service", stagedCommand);
  }

  [WindowsOnlyFact]
  public async Task RetirePreviousBrand_RunsItsUninstallAndDeletesItsSettingsDirectory()
  {
    var previousBrand = "Acme Remote";
    var previousInstallDirectory = @"C:\Program Files\Acme_Remote\instance-1";
    // The replaced install's executable is named for its own brand, not for this build's.
    var previousAgentPath = Path.Combine(previousInstallDirectory, "Acme_Remote.Agent.exe");
    var previousSettingsDirectory = @"C:\ProgramData\Acme_Remote\instance-1";
    var fileSystem = new FakeFileSystem('\\');
    var processManager = new Mock<IProcessManager>();
    var pathProvider = new Mock<IFileSystemPathProvider>();
    var systemEnvironment = new Mock<ISystemEnvironment>();
    var stagedPath = string.Empty;
    var stagedCommand = string.Empty;

    fileSystem.AddFile(previousAgentPath, []);
    fileSystem.AddDirectory(previousSettingsDirectory);
    fileSystem.AddDirectory(Path.GetTempPath());

    systemEnvironment.SetupGet(x => x.Platform).Returns(SystemPlatform.Windows);
    pathProvider
      .Setup(x => x.GetAgentInstallDirectoryFor(previousBrand, It.IsAny<string?>()))
      .Returns(previousInstallDirectory);
    pathProvider
      .Setup(x => x.GetSettingsDirectoryFor(previousBrand, It.IsAny<string?>()))
      .Returns(previousSettingsDirectory);

    processManager
      .Setup(x => x.StartAndWaitForExit(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<TimeSpan>()))
      .Returns<string, string, bool, TimeSpan>((fileName, arguments, _, _) =>
      {
        stagedPath = fileName;
        stagedCommand = arguments;
        fileSystem.AddFile(fileName, []);
        return Task.FromResult(0);
      });

    var sut = CreateSut(fileSystem, processManager, new Mock<IRetryer>(), pathProvider, systemEnvironment);

    var result = await sut.RetirePreviousBrand(previousBrand);

    Assert.True(result.IsSuccess);
    Assert.Equal("uninstall", stagedCommand);

    // Run from its own install directory, that agent copies itself elsewhere and returns before doing
    // anything, so the command has to run against a staged copy instead.
    Assert.NotEqual(previousAgentPath, stagedPath);
    Assert.StartsWith(Path.GetTempPath(), stagedPath, StringComparison.OrdinalIgnoreCase);

    // The retired install's uninstall keeps its settings directory, which holds the signing key.
    Assert.False(fileSystem.DirectoryExists(previousSettingsDirectory));
  }

  [WindowsOnlyFact]
  public async Task StopPreviousBrand_WhenTheReplacedInstallCanBeStopped_ReportsSuccess()
  {
    var (sut, previousBrand) = CreateStopPreviousBrandSetup(exitCode: 0);

    var result = await sut.StopPreviousBrand(previousBrand);

    Assert.True(result.IsSuccess);
  }

  [WindowsOnlyFact]
  public async Task StopPreviousBrand_WhenTheReplacedInstallCannotBeStopped_ReportsFailure()
  {
    var (sut, previousBrand) = CreateStopPreviousBrandSetup(exitCode: 1);

    var result = await sut.StopPreviousBrand(previousBrand);

    // Both installs would keep the device's connection and sign as the same device, so the caller has
    // to be able to refuse to continue rather than treat this as a warning.
    Assert.False(result.IsSuccess);
  }

  [WindowsOnlyFact]
  public async Task Uninstall_DoesNotClearMachineWideSoftwareSasGeneration()
  {
    var installDir = Path.Combine(Path.GetTempPath(), "ControlR", "Install", AppConstants.DefaultInstanceId);
    var fileSystem = new FakeFileSystem('\\');
    var registryAccessor = new Mock<IRegistryAccessor>();
    var processManager = new Mock<IProcessManager>();
    var pathProvider = new Mock<IFileSystemPathProvider>();
    var systemEnvironment = new Mock<ISystemEnvironment>();
    var elevationChecker = new Mock<IElevationChecker>();

    fileSystem.AddDirectory(installDir);
    fileSystem.AddDirectory(Path.Combine(installDir, "DesktopClient"));

    systemEnvironment.SetupGet(x => x.IsDebug).Returns(true);
    systemEnvironment.SetupGet(x => x.StartupDirectory).Returns(@"C:\somewhere-else");
    systemEnvironment.SetupGet(x => x.Platform).Returns(SystemPlatform.Windows);

    elevationChecker.Setup(x => x.IsElevated()).Returns(true);

    pathProvider.Setup(x => x.GetAgentInstallDirectory()).Returns(installDir);
    pathProvider
      .Setup(x => x.GetUninstallKeyPath())
      .Returns(@"SOFTWARE\ControlR.Tests.NotFound");

    processManager
      .Setup(x => x.GetProcessesByName(It.IsAny<string>()))
      .Returns([]);
    processManager
      .Setup(x => x.GetProcessOutput("cmd.exe", It.IsAny<string>()))
      .ReturnsAsync(Result.Ok(string.Empty));

    var sut = CreateSut(
      fileSystem,
      processManager,
      new Mock<IRetryer>(),
      pathProvider,
      systemEnvironment,
      registryAccessor,
      elevationChecker);

    await sut.Uninstall();

    // SoftwareSASGeneration is a single machine-wide policy, not one value per install. Clearing it on
    // one uninstall switches off Ctrl + Alt + Del simulation for every agent still on the machine, and
    // the failure is swallowed where SAS is generated, so nobody sees why it stopped working.
    registryAccessor.Verify(x => x.SetSoftwareSasGeneration(It.IsAny<bool>()), Times.Never);
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

  private static (TestableAgentInstallerWindows Sut, string PreviousBrand) CreateStopPreviousBrandSetup(int exitCode)
  {
    const string previousBrand = "Acme Remote";
    var previousInstallDirectory = @"C:\Program Files\Acme_Remote\instance-1";
    var previousAgentPath = Path.Combine(previousInstallDirectory, "Acme_Remote.Agent.exe");
    var fileSystem = new FakeFileSystem('\\');
    var processManager = new Mock<IProcessManager>();
    var pathProvider = new Mock<IFileSystemPathProvider>();
    var systemEnvironment = new Mock<ISystemEnvironment>();

    fileSystem.AddFile(previousAgentPath, []);
    fileSystem.AddDirectory(Path.GetTempPath());

    systemEnvironment.SetupGet(x => x.Platform).Returns(SystemPlatform.Windows);
    pathProvider
      .Setup(x => x.GetAgentInstallDirectoryFor(previousBrand, It.IsAny<string?>()))
      .Returns(previousInstallDirectory);

    processManager
      .Setup(x => x.StartAndWaitForExit(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<TimeSpan>()))
      .Returns<string, string, bool, TimeSpan>((fileName, _, _, _) =>
      {
        fileSystem.AddFile(fileName, []);
        return Task.FromResult(exitCode);
      });

    var sut = CreateSut(fileSystem, processManager, new Mock<IRetryer>(), pathProvider, systemEnvironment);
    return (sut, previousBrand);
  }

  private static TestableAgentInstallerWindows CreateSut(
    IFileSystem fileSystem,
    Mock<IProcessManager> processManager,
    Mock<IRetryer> retryer,
    Mock<IFileSystemPathProvider> pathProvider,
    Mock<ISystemEnvironment> systemEnvironment,
    Mock<IRegistryAccessor>? registryAccessor = null,
    Mock<IElevationChecker>? elevationChecker = null)
  {
    return new TestableAgentInstallerWindows(
      Mock.Of<IHostApplicationLifetime>(),
      processManager.Object,
      systemEnvironment.Object,
      elevationChecker?.Object ?? Mock.Of<IElevationChecker>(),
      retryer.Object,
      Mock.Of<IControlrApi>(),
      Mock.Of<IDeviceInfoProvider>(),
      pathProvider.Object,
      registryAccessor?.Object ?? Mock.Of<IRegistryAccessor>(),
      Microsoft.Extensions.Options.Options.Create(new InstanceOptions()),
      fileSystem,
      Mock.Of<IOptionsAccessor>(),
      Mock.Of<IOptionsMonitor<AgentAppOptions>>(),
      Mock.Of<IEd25519KeyProvider>(),
      NullLogger<AgentInstallerWindows>.Instance);
  }

  private sealed class TestableAgentInstallerWindows(
    IHostApplicationLifetime lifetime,
    IProcessManager processManager,
    ISystemEnvironment systemEnvironment,
    IElevationChecker elevationChecker,
    IRetryer retryer,
    IControlrApi controlrApi,
    IDeviceInfoProvider deviceDataGenerator,
    IFileSystemPathProvider fileSystemPathProvider,
    IRegistryAccessor registryAccessor,
    IOptions<InstanceOptions> instanceOptions,
    IFileSystem fileSystem,
    IOptionsAccessor optionsAccessor,
    IOptionsMonitor<AgentAppOptions> appOptions,
    IEd25519KeyProvider keyProvider,
    ILogger<AgentInstallerWindows> logger)
    : AgentInstallerWindows(
      lifetime,
      processManager,
      systemEnvironment,
      elevationChecker,
      retryer,
      controlrApi,
      deviceDataGenerator,
      fileSystemPathProvider,
      registryAccessor,
      instanceOptions,
      fileSystem,
      optionsAccessor,
      appOptions,
      keyProvider,
      logger)
  {
    public Task<Result> StopPreviousBrand(string? previousBrandName)
    {
      return StopPreviousBrandService(previousBrandName);
    }
  }
}