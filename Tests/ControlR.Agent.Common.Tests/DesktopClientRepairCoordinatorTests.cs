using ControlR.Agent.Common.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace ControlR.Agent.Common.Tests;

public class DesktopClientRepairCoordinatorTests
{
  [Fact]
  public async Task ReportFailure_WhenConcurrentImmediateFailures_QueuesSingleRepair()
  {
    var cancellationToken = TestContext.Current.CancellationToken;
    var repairStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var repairCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var repairCalls = 0;
    var updater = new Mock<IAgentMaintenanceService>();
    updater
      .Setup(x => x.RepairDesktopClient(It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .Returns<string, CancellationToken>((_, _) =>
      {
        if (Interlocked.Increment(ref repairCalls) == 1)
        {
          repairStarted.TrySetResult();
        }

        return repairCompletion.Task;
      });

    var appLifetime = new Mock<IHostApplicationLifetime>();
    appLifetime.SetupGet(x => x.ApplicationStopping).Returns(CancellationToken.None);

    var sut = new DesktopClientRepairCoordinator(
      new FakeTimeProvider(DateTimeOffset.UtcNow),
      updater.Object,
      appLifetime.Object,
      NullLogger<DesktopClientRepairCoordinator>.Instance);

    await Task.WhenAll(
      Enumerable.Range(0, 20)
        .Select(i => Task.Run(() => sut.ReportFailure($"desktop-{i}", "desktop launch failed", immediate: true), cancellationToken)));

    await repairStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
    await Task.Delay(100, cancellationToken);

    Assert.Equal(1, Volatile.Read(ref repairCalls));

    repairCompletion.TrySetResult();
    await Task.Delay(50, cancellationToken);
  }
}