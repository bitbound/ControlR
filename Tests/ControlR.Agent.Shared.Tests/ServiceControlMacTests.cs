using ControlR.Agent.Shared.Options;
using ControlR.Agent.Shared.Services.Mac;
using ControlR.Libraries.Shared.Services.Processes;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Diagnostics;

namespace ControlR.Agent.Shared.Tests;

public class ServiceControlMacTests
{
  [Fact]
  public async Task StartAgentService_WhenTheStartTimesOut_StaysQuietWhenTheCallerDidNotAskToBeTold()
  {
    var processManager = new Mock<IProcessManager>();
    processManager
      .Setup(x => x.StartAndWaitForExit(It.IsAny<ProcessStartInfo>(), It.IsAny<TimeSpan>()))
      .Returns(Task.FromException<int>(new OperationCanceledException()));

    var sut = new ServiceControlMac(
      processManager.Object,
      Microsoft.Extensions.Options.Options.Create(new InstanceOptions()),
      NullLogger<ServiceControlMac>.Instance);

    await sut.StartAgentService(throwOnFailure: false);
  }

  [Fact]
  public async Task StartAgentService_WhenTheStartTimesOut_ThrowsWhenTheCallerAskedToBeTold()
  {
    var processManager = new Mock<IProcessManager>();
    processManager
      .Setup(x => x.StartAndWaitForExit(It.IsAny<ProcessStartInfo>(), It.IsAny<TimeSpan>()))
      .Returns(Task.FromException<int>(new OperationCanceledException()));

    var sut = new ServiceControlMac(
      processManager.Object,
      Microsoft.Extensions.Options.Options.Create(new InstanceOptions()),
      NullLogger<ServiceControlMac>.Instance);

    // A migration removes the install being replaced once the new install reports it is running, so a
    // start that timed out has to reach a caller that asked for failures instead of reading as success.
    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sut.StartAgentService(throwOnFailure: true));
  }
}
