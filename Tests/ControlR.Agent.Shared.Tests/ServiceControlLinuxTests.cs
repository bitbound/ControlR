using ControlR.Agent.Shared.Options;
using ControlR.Agent.Shared.Services.Linux;
using ControlR.Libraries.Shared.Services.Processes;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ControlR.Agent.Shared.Tests;

public class ServiceControlLinuxTests
{
  [Fact]
  public async Task StartAgentService_WhenTheUnitNeverReportsActive_StaysQuietWhenTheCallerDidNotAskToBeTold()
  {
    var sut = CreateSut(startExitCode: 0, isActiveExitCode: 1);

    await sut.StartAgentService(throwOnFailure: false);
  }

  [Fact]
  public async Task StartAgentService_WhenTheUnitNeverReportsActive_ThrowsWhenTheCallerAskedToBeTold()
  {
    var sut = CreateSut(startExitCode: 0, isActiveExitCode: 1);

    // A zero exit only means systemd accepted the start job. These units are Type=simple, so the
    // process can die straight after the fork, and a caller that removes the install being replaced
    // on the strength of that exit would leave the machine with no agent.
    await Assert.ThrowsAsync<InvalidOperationException>(() => sut.StartAgentService(throwOnFailure: true));
  }

  [Fact]
  public async Task StartAgentService_WhenTheUnitReportsActive_ConfirmsItAndSucceeds()
  {
    var processManager = new Mock<IProcessManager>();
    processManager
      .Setup(x => x.StartAndWaitForExit(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<TimeSpan>()))
      .ReturnsAsync(0);

    var sut = CreateSut(processManager);

    await sut.StartAgentService(throwOnFailure: true);

    processManager.Verify(
      x => x.StartAndWaitForExit(
        "sudo",
        It.Is<string>(arguments => arguments.Contains("is-active", StringComparison.Ordinal)),
        false,
        It.IsAny<TimeSpan>()),
      Times.Once);
  }

  private static ServiceControlLinux CreateSut(int startExitCode, int isActiveExitCode)
  {
    var processManager = new Mock<IProcessManager>();
    processManager
      .Setup(x => x.StartAndWaitForExit(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<TimeSpan>()))
      .Returns<string, string, bool, TimeSpan>((_, arguments, _, _) =>
        Task.FromResult(arguments.Contains("is-active", StringComparison.Ordinal) ? isActiveExitCode : startExitCode));

    return CreateSut(processManager);
  }

  private static ServiceControlLinux CreateSut(Mock<IProcessManager> processManager)
  {
    return new ServiceControlLinux(
      processManager.Object,
      Mock.Of<IHeadlessServerDetector>(),
      Mock.Of<ILoggedInUserProvider>(),
      Microsoft.Extensions.Options.Options.Create(new InstanceOptions { InstanceId = "exp" }),
      NullLogger<ServiceControlLinux>.Instance);
  }
}
