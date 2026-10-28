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

  [Fact]
  public async Task StopPreviousBrandDesktopClientService_StopsTheReplacedBrandsUnitForEveryLoggedInUser()
  {
    var processManager = new Mock<IProcessManager>();
    processManager
      .Setup(x => x.StartAndWaitForExit(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<TimeSpan>()))
      .ReturnsAsync(0);

    var loggedInUsers = new Mock<ILoggedInUserProvider>();
    loggedInUsers
      .Setup(x => x.GetLoggedInUsers())
      .ReturnsAsync([new LoggedInUserSession("1000", "x11", IsActive: false)]);

    var sut = new ServiceControlLinux(
      processManager.Object,
      Mock.Of<IHeadlessServerDetector>(),
      loggedInUsers.Object,
      Microsoft.Extensions.Options.Options.Create(new InstanceOptions { InstanceId = "exp" }),
      NullLogger<ServiceControlLinux>.Instance);

    await sut.StopPreviousBrandDesktopClientService("Acme Remote", "exp", throwOnFailure: true);

    // The replaced install's unit is named for its own brand and the instance id it was created
    // with, so it is never the current brand's unit name. Inactive sessions are included because a
    // client left running in a switched VT session must not survive to be relaunched either.
    processManager.Verify(
      x => x.StartAndWaitForExit(
        "sudo",
        "-u #1000 XDG_RUNTIME_DIR=/run/user/1000 systemctl --user stop acme_remote.desktop-exp.service",
        false,
        It.IsAny<TimeSpan>()),
      Times.Once);
  }

  [Theory]
  [InlineData(true)]
  [InlineData(false)]
  public async Task StopDesktopClientService_WhenAUserUnitStopFails_ReportsItOnlyToCallersThatAsked(bool throwOnFailure)
  {
    var processManager = new Mock<IProcessManager>();
    processManager
      .Setup(x => x.StartAndWaitForExit(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<TimeSpan>()))
      .ReturnsAsync(1);

    var loggedInUsers = new Mock<ILoggedInUserProvider>();
    loggedInUsers
      .Setup(x => x.GetLoggedInUsers())
      .ReturnsAsync([new LoggedInUserSession("1000", "x11", IsActive: false)]);

    var sut = new ServiceControlLinux(
      processManager.Object,
      Mock.Of<IHeadlessServerDetector>(),
      loggedInUsers.Object,
      Microsoft.Extensions.Options.Options.Create(new InstanceOptions { InstanceId = "exp" }),
      NullLogger<ServiceControlLinux>.Instance);

    // The installer kills client processes after stopping their unit, and a SIGKILL against a unit
    // still active is just a Restart=on-failure. The kill's caller has to be able to learn that the
    // stop never landed, which starts with a nonzero systemctl exit not reading as success.
    if (throwOnFailure)
    {
      await Assert.ThrowsAsync<InvalidOperationException>(() => sut.StopDesktopClientService(throwOnFailure: true));
    }
    else
    {
      await sut.StopDesktopClientService(throwOnFailure: false);
    }
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
