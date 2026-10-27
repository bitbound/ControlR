using System.Runtime.Versioning;
using ControlR.Agent.Shared.Interfaces;
using ControlR.Agent.Shared.Models;
using ControlR.Agent.Shared.Options;
using ControlR.Agent.Shared.Services;
using ControlR.Agent.Shared.Services.Linux;
using ControlR.ApiClient;
using ControlR.Libraries.Shared.Services;
using ControlR.Libraries.Shared.Services.Encryption;
using ControlR.Libraries.Shared.Services.FileSystem;
using ControlR.Libraries.Shared.Services.Processes;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace ControlR.Agent.Shared.Tests;

[SupportedOSPlatform("linux")]
public class AgentInstallerLinuxTests
{
  [Fact]
  public void GetPreviousBrandDesktopClientPath_UsesThePreviousBrandAndInstance()
  {
    var pathProvider = new Mock<IFileSystemPathProvider>();
    pathProvider
      .Setup(x => x.GetAgentInstallDirectoryFor("Acme Remote", "exp"))
      .Returns("/usr/local/bin/Acme_Remote/exp");

    var sut = CreateSut(
      Mock.Of<IFileSystem>(),
      Mock.Of<IProcessManager>(),
      Mock.Of<ISystemEnvironment>(),
      pathProvider.Object,
      Mock.Of<IElevationChecker>());

    var path = sut.GetPreviousBrandDesktopClientPath("Acme Remote", "exp");

    // The replaced install's client lives under its own brand key and instance id, and is named for
    // that brand, so a migration that changes either one still finds it.
    Assert.Equal(
      Path.Combine("/usr/local/bin/Acme_Remote/exp", "DesktopClient", "Acme_Remote.DesktopClient"),
      path);
  }

  [Fact]
  public async Task Install_KillsRunningDesktopClientBeforeReplacingTheDirectory()
  {
    var installDir = "/usr/local/bin/ControlR/default";
    var fileSystem = new Mock<IFileSystem>();
    var processManager = new Mock<IProcessManager>();
    var systemEnvironment = new Mock<ISystemEnvironment>();
    var pathProvider = new Mock<IFileSystemPathProvider>();
    var elevationChecker = new Mock<IElevationChecker>();
    var processLocator = new Mock<ILinuxDesktopClientProcessLocator>();

    elevationChecker.Setup(x => x.IsElevated()).Returns(true);
    pathProvider.Setup(x => x.GetAgentInstallDirectory()).Returns(installDir);
    fileSystem.Setup(x => x.DirectoryExists(installDir)).Returns(true);

    var stopProcess = new Mock<IProcess>();
    stopProcess.Setup(x => x.WaitForExitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
    processManager.Setup(x => x.Start("sudo", It.IsAny<string>())).Returns(stopProcess.Object);

    // A login screen client this install left running, which would be orphaned once the directory is
    // replaced. The agent that spawned it is already stopped by the time the installer gets here.
    var client = new Mock<IProcess>();
    processLocator
      .Setup(x => x.FindByExecutablePath(It.IsAny<string>()))
      .Returns([new LinuxDesktopClientProcess(4242, 0, "/usr/local/bin/ControlR/default/DesktopClient/ControlR.DesktopClient (deleted)")]);
    processLocator
      .Setup(x => x.GetRunningExecutableOwnerUid(4242, It.IsAny<string>()))
      .Returns(0);
    processManager.Setup(x => x.GetProcessById(4242)).Returns(client.Object);

    // Cut the run short once the directory would be recreated, after the kill has happened.
    fileSystem.Setup(x => x.CreateDirectory(installDir)).Throws(new IOException("not part of this test"));

    var sut = CreateSut(
      fileSystem.Object,
      processManager.Object,
      systemEnvironment.Object,
      pathProvider.Object,
      elevationChecker.Object,
      processLocator.Object);

    await sut.Install(CreateRequest());

    client.Verify(x => x.Kill(), Times.Once);
  }

  [Fact]
  public async Task Install_LeavesTheInstallDirectoryBeforeDeletingIt()
  {
    var installDir = "/usr/local/bin/ControlR/default";
    var fileSystem = new Mock<IFileSystem>();
    var processManager = new Mock<IProcessManager>();
    var systemEnvironment = new Mock<ISystemEnvironment>();
    var pathProvider = new Mock<IFileSystemPathProvider>();
    var elevationChecker = new Mock<IElevationChecker>();
    var steps = new List<string>();

    elevationChecker.Setup(x => x.IsElevated()).Returns(true);
    pathProvider.Setup(x => x.GetAgentInstallDirectory()).Returns(installDir);
    fileSystem.Setup(x => x.DirectoryExists(installDir)).Returns(true);

    var stopProcess = new Mock<IProcess>();
    stopProcess.Setup(x => x.WaitForExitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
    processManager.Setup(x => x.Start("sudo", It.IsAny<string>())).Returns(stopProcess.Object);

    string? leftDirectory = null;
    systemEnvironment
      .Setup(x => x.SetWorkingDirectory(It.IsAny<string>()))
      .Callback<string>(path =>
      {
        leftDirectory = path;
        steps.Add("leave");
      });

    fileSystem
      .Setup(x => x.DeleteDirectory(installDir, true))
      .Callback(() => steps.Add("delete"));

    // Nothing after the delete is part of this assertion, so cut the run short once it happens.
    fileSystem.Setup(x => x.CreateDirectory(installDir)).Throws(new IOException("not part of this test"));

    var sut = CreateSut(
      fileSystem.Object,
      processManager.Object,
      systemEnvironment.Object,
      pathProvider.Object,
      elevationChecker.Object);

    await sut.Install(CreateRequest());

    // A process whose working directory has been deleted cannot resolve a relative file name, so the
    // systemctl calls that follow the delete throw instead of starting the service. The install is
    // already on disk and enabled by then, and the machine is left with no running agent.
    Assert.Equal(["leave", "delete"], steps);

    Assert.NotNull(leftDirectory);
    Assert.False(leftDirectory.StartsWith(installDir, StringComparison.Ordinal));
  }

  [Fact]
  public async Task RepairDesktopClient_KillsRunningDesktopClientBeforeReplacingThePayload()
  {
    var installDir = "/usr/local/bin/ControlR/default";
    var fileSystem = new Mock<IFileSystem>();
    var processManager = new Mock<IProcessManager>();
    var pathProvider = new Mock<IFileSystemPathProvider>();
    var elevationChecker = new Mock<IElevationChecker>();
    var processLocator = new Mock<ILinuxDesktopClientProcessLocator>();

    elevationChecker.Setup(x => x.IsElevated()).Returns(true);
    pathProvider.Setup(x => x.GetAgentInstallDirectory()).Returns(installDir);
    fileSystem.Setup(x => x.FileExists(It.IsAny<string>())).Returns(true);

    // Repair moves the live DesktopClient directory aside rather than stopping the agent first, so a
    // greeter client still running from it is orphaned unless something kills it here.
    var client = new Mock<IProcess>();
    processLocator
      .Setup(x => x.FindByExecutablePath(It.IsAny<string>()))
      .Returns([new LinuxDesktopClientProcess(4242, 0, "/usr/local/bin/ControlR/default/DesktopClient/ControlR.DesktopClient")]);
    processLocator
      .Setup(x => x.GetRunningExecutableOwnerUid(4242, It.IsAny<string>()))
      .Returns(0);
    processManager.Setup(x => x.GetProcessById(4242)).Returns(client.Object);

    var sut = CreateSut(
      fileSystem.Object,
      processManager.Object,
      Mock.Of<ISystemEnvironment>(),
      pathProvider.Object,
      elevationChecker.Object,
      processLocator.Object);

    await sut.RepairDesktopClient(CreateRequest());

    client.Verify(x => x.Kill(), Times.Once);
  }

  [Fact]
  public async Task Uninstall_KillsRunningDesktopClient()
  {
    var installDir = "/usr/local/bin/ControlR/default";
    var fileSystem = new Mock<IFileSystem>();
    var processManager = new Mock<IProcessManager>();
    var pathProvider = new Mock<IFileSystemPathProvider>();
    var elevationChecker = new Mock<IElevationChecker>();
    var processLocator = new Mock<ILinuxDesktopClientProcessLocator>();

    elevationChecker.Setup(x => x.IsElevated()).Returns(true);
    pathProvider.Setup(x => x.GetAgentInstallDirectory()).Returns(installDir);

    var stopProcess = new Mock<IProcess>();
    stopProcess.Setup(x => x.WaitForExitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
    processManager.Setup(x => x.Start(It.IsAny<string>(), It.IsAny<string>())).Returns(stopProcess.Object);

    // Uninstall leaves no agent behind to reconcile the greeter session, so the client this install
    // spawned has to be taken down as part of removing the install.
    var client = new Mock<IProcess>();
    processLocator
      .Setup(x => x.FindByExecutablePath(It.IsAny<string>()))
      .Returns([new LinuxDesktopClientProcess(4242, 0, "/usr/local/bin/ControlR/default/DesktopClient/ControlR.DesktopClient")]);
    processLocator
      .Setup(x => x.GetRunningExecutableOwnerUid(4242, It.IsAny<string>()))
      .Returns(0);
    processManager.Setup(x => x.GetProcessById(4242)).Returns(client.Object);

    var sut = CreateSut(
      fileSystem.Object,
      processManager.Object,
      Mock.Of<ISystemEnvironment>(),
      pathProvider.Object,
      elevationChecker.Object,
      processLocator.Object);

    await sut.Uninstall();

    client.Verify(x => x.Kill(), Times.Once);
  }

  [Fact]
  public async Task Uninstall_WhenNotRoot_ChangesNothing()
  {
    var installDir = "/usr/local/bin/ControlR/default";
    var fileSystem = new Mock<IFileSystem>();
    var processManager = new Mock<IProcessManager>();
    var pathProvider = new Mock<IFileSystemPathProvider>();
    var elevationChecker = new Mock<IElevationChecker>();

    elevationChecker.Setup(x => x.IsElevated()).Returns(false);
    pathProvider.Setup(x => x.GetAgentInstallDirectory()).Returns(installDir);

    var stopProcess = new Mock<IProcess>();
    stopProcess.Setup(x => x.WaitForExitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
    processManager.Setup(x => x.Start(It.IsAny<string>(), It.IsAny<string>())).Returns(stopProcess.Object);

    var sut = CreateSut(
      fileSystem.Object,
      processManager.Object,
      Mock.Of<ISystemEnvironment>(),
      pathProvider.Object,
      elevationChecker.Object);

    await sut.Uninstall();

    // Without root none of the systemctl calls can work, so continuing past this point spends the
    // rest of the run failing, and ends by deleting the install directory anyway.
    processManager.Verify(x => x.Start(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    fileSystem.Verify(x => x.DeleteDirectory(installDir, true), Times.Never);
  }

  private static AgentInstallRequest CreateRequest()
  {
    return new AgentInstallRequest
    {
      BundleZipPath = "/tmp/controlr-install-abcdef/ControlR.Agent.bundle.zip",
      BundleSha256 = "hash",
      DeviceId = Guid.NewGuid(),
      ServerUri = new Uri("https://example.test"),
      TenantId = Guid.NewGuid()
    };
  }

  private static AgentInstallerLinux CreateSut(
    IFileSystem fileSystem,
    IProcessManager processManager,
    ISystemEnvironment systemEnvironment,
    IFileSystemPathProvider pathProvider,
    IElevationChecker elevationChecker,
    ILinuxDesktopClientProcessLocator? processLocator = null)
  {
    if (processLocator is null)
    {
      var defaultLocator = new Mock<ILinuxDesktopClientProcessLocator>();
      defaultLocator
        .Setup(x => x.FindByExecutablePath(It.IsAny<string>()))
        .Returns([]);
      processLocator = defaultLocator.Object;
    }

    return new AgentInstallerLinux(
      Mock.Of<IHostApplicationLifetime>(),
      fileSystem,
      pathProvider,
      processManager,
      systemEnvironment,
      Mock.Of<IControlrApi>(),
      Mock.Of<IDeviceInfoProvider>(),
      Mock.Of<IRetryer>(),
      Mock.Of<IOptionsAccessor>(),
      elevationChecker,
      Mock.Of<IServiceControl>(),
      Mock.Of<IEmbeddedResourceAccessor>(),
      Mock.Of<IOptionsMonitor<AgentAppOptions>>(),
      Microsoft.Extensions.Options.Options.Create(new InstanceOptions()),
      Mock.Of<IEd25519KeyProvider>(),
      processLocator,
      NullLogger<AgentInstallerLinux>.Instance);
  }
}
