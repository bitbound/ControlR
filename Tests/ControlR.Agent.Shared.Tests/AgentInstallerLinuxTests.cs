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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace ControlR.Agent.Shared.Tests;

[SupportedOSPlatform("linux")]
public class AgentInstallerLinuxTests
{
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
    IElevationChecker elevationChecker)
  {
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
      NullLogger<AgentInstallerLinux>.Instance);
  }
}
