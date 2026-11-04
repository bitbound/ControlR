using ControlR.Agent.Shared.Interfaces;
using ControlR.Agent.Shared.Options;
using ControlR.Agent.Shared.Services;
using ControlR.Agent.Shared.Services.Base;
using ControlR.ApiClient;
using ControlR.Libraries.Branding;
using ControlR.Libraries.Shared.Constants;
using ControlR.Libraries.Shared.Primitives;
using ControlR.Libraries.Shared.Services;
using ControlR.Libraries.Shared.Services.Encryption;
using ControlR.Libraries.Shared.Services.FileSystem;
using ControlR.Libraries.Shared.Services.Processes;
using ControlR.Libraries.TestingUtilities.FileSystem;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace ControlR.Agent.Shared.Tests;

public class AgentInstallerBaseTests
{
  [Fact]
  public void GetInstanceInstallDirectory_WhenInstanceIdMissing_UsesDefaultSubdirectory()
  {
    var result = TestAgentInstaller.GetInstallDirectoryForTest(@"C:\Program Files\ControlR", instanceId: null);

    Assert.Equal(Path.Combine(@"C:\Program Files\ControlR", AppConstants.DefaultInstanceId), result);
  }

  [Fact]
  public async Task RetirePreviousBrand_WhenOnlyThePublisherDiffers_LeavesTheReplacedInstallServiceAlone()
  {
    const string previousPublisher = "Acme Corp";
    var previousInstallDirectory = "/usr/local/bin/acme_corp/controlr/default";
    var previousSettingsDirectory = "/etc/acme_corp/controlr/default";
    var fileSystem = new FakeFileSystem('/');
    var processManager = new Mock<IProcessManager>();
    var pathProvider = new Mock<IFileSystemPathProvider>();

    fileSystem.AddFile($"{previousInstallDirectory}/ControlR.Agent", []);
    fileSystem.AddDirectory(previousSettingsDirectory);

    pathProvider.Setup(x => x.GetEffectiveInstanceId()).Returns(AppConstants.DefaultInstanceId);
    pathProvider
      .Setup(x => x.GetAgentInstallDirectoryFor(previousPublisher, BrandingConstants.BrandName, null))
      .Returns(previousInstallDirectory);
    pathProvider
      .Setup(x => x.GetSettingsDirectoryFor(previousPublisher, BrandingConstants.BrandName, null))
      .Returns(previousSettingsDirectory);

    var sut = CreateInstaller(fileSystem, processManager.Object, pathProvider.Object);

    var result = await sut.RetirePreviousBrand(previousPublisher, BrandingConstants.BrandName, previousInstanceId: null);

    // Only the directories carry the publisher, so this install and the one being replaced were
    // registered under the same service name. Running the replaced binary's uninstall would remove the
    // service this install just took over, so that install goes by deleting its files.
    Assert.True(result.IsSuccess);
    processManager.Verify(
      x => x.StartAndWaitForExit(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<TimeSpan>()),
      Times.Never);
    Assert.False(fileSystem.DirectoryExists(previousInstallDirectory));
    Assert.False(fileSystem.DirectoryExists(previousSettingsDirectory));
  }

  [Fact]
  public void StopProcesses_KillsOnlyProcessesForCurrentInstallDirectory()
  {
    var currentAgentProcessId = Environment.ProcessId;
    var targetAgentPath = @"C:\Program Files\ControlR\default\ControlR.Agent.exe";
    var targetDesktopClientPath = @"C:\Program Files\ControlR\default\DesktopClient\ControlR.DesktopClient.exe";
    var matchingAgent = CreateProcess(101, targetAgentPath);
    var currentAgent = CreateProcess(currentAgentProcessId, targetAgentPath);
    var otherAgent = CreateProcess(102, @"C:\Program Files\ControlR\c.jaredg.dev\ControlR.Agent.exe");
    var matchingDesktop = CreateProcess(201, targetDesktopClientPath);
    var otherDesktop = CreateProcess(202, @"C:\Program Files\ControlR\c.jaredg.dev\DesktopClient\ControlR.DesktopClient.exe");
    var processManager = new Mock<IProcessManager>();

    processManager
      .Setup(x => x.GetProcessesByName("ControlR.Agent"))
      .Returns([matchingAgent.Object, currentAgent.Object, otherAgent.Object]);

    processManager
      .Setup(x => x.GetProcessesByName("ControlR.DesktopClient"))
      .Returns([matchingDesktop.Object, otherDesktop.Object]);

    var systemEnvironment = new Mock<ISystemEnvironment>();
    systemEnvironment.SetupGet(x => x.ProcessId).Returns(currentAgentProcessId);

    var sut = new TestAgentInstaller(processManager.Object, systemEnvironment.Object);

    var result = sut.StopProcessesForTest(targetAgentPath, targetDesktopClientPath);

    Assert.True(result.IsSuccess);
    matchingAgent.Verify(x => x.Kill(), Times.Once);
    currentAgent.Verify(x => x.Kill(), Times.Never);
    otherAgent.Verify(x => x.Kill(), Times.Never);
    matchingDesktop.Verify(x => x.Kill(), Times.Once);
    otherDesktop.Verify(x => x.Kill(), Times.Never);
  }

  private static TestAgentInstaller CreateInstaller(
    IFileSystem fileSystem,
    IProcessManager processManager,
    IFileSystemPathProvider pathProvider)
  {
    return new TestAgentInstaller(
      processManager,
      Mock.Of<ISystemEnvironment>(),
      fileSystem,
      pathProvider);
  }

  private static Mock<IProcess> CreateProcess(int processId, string filePath)
  {
    var process = new Mock<IProcess>();
    process.SetupGet(x => x.Id).Returns(processId);
    process.SetupGet(x => x.FilePath).Returns(filePath);
    return process;
  }

  private sealed class TestAgentInstaller(
    IProcessManager processManager,
    ISystemEnvironment systemEnvironment,
    IFileSystem? fileSystem = null,
    IFileSystemPathProvider? pathProvider = null)
    : AgentInstallerBase(
      fileSystem ?? Mock.Of<IFileSystem>(),
      pathProvider ?? Mock.Of<IFileSystemPathProvider>(),
      Mock.Of<IControlrApi>(),
      Mock.Of<IDeviceInfoProvider>(),
      Mock.Of<IOptionsAccessor>(),
      processManager,
      systemEnvironment,
      Mock.Of<IOptionsMonitor<AgentAppOptions>>(),
      NullLogger<AgentInstallerBase>.Instance,
      Mock.Of<IEd25519KeyProvider>())
  {
    public static string GetInstallDirectoryForTest(string rootDirectory, string? instanceId)
    {
      return GetInstanceInstallDirectory(rootDirectory, instanceId);
    }

    public Result StopProcessesForTest(string targetAgentPath, string targetDesktopClientPath)
    {
      return StopProcesses(targetAgentPath, targetDesktopClientPath);
    }
  }
}