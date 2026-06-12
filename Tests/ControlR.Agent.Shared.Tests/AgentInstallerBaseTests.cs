using ControlR.Agent.Shared.Interfaces;
using ControlR.Agent.Shared.Models;
using ControlR.Agent.Shared.Options;
using ControlR.Agent.Shared.Services;
using ControlR.Agent.Shared.Services.Base;
using ControlR.ApiClient;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi;
using ControlR.Libraries.Shared.Constants;
using ControlR.Libraries.Shared.Primitives;
using ControlR.Libraries.Shared.Services;
using ControlR.Libraries.Shared.Services.Encryption;
using ControlR.Libraries.Shared.Services.FileSystem;
using ControlR.Libraries.Shared.Services.Processes;
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

    Assert.Equal(Path.Combine(@"C:\Program Files\ControlR", AppConstants.DefaultInstallDirectoryName), result);
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

  private static Mock<IProcess> CreateProcess(int processId, string filePath)
  {
    var process = new Mock<IProcess>();
    process.SetupGet(x => x.Id).Returns(processId);
    process.SetupGet(x => x.FilePath).Returns(filePath);
    return process;
  }

  private sealed class TestAgentInstaller(IProcessManager processManager, ISystemEnvironment systemEnvironment)
    : AgentInstallerBase(
      Mock.Of<IFileSystem>(),
      Mock.Of<IFileSystemPathProvider>(),
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