using Avalonia.Controls.ApplicationLifetimes;
using ControlR.DesktopClient.Linux.Services;
using ControlR.Libraries.NativeInterop.Linux;
using ControlR.Libraries.Shared.Constants;
using ControlR.Libraries.TestingUtilities.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace ControlR.DesktopClient.Linux.Tests;

public class AgentLifetimeWatchdogTests
{
  private const int AgentPid = 4242;

  public AgentLifetimeWatchdogTests()
  {
    Environment.SetEnvironmentVariable(AppConstants.AgentPidEnvironmentVariable, null);
  }

  [Fact]
  public async Task ExecuteAsync_ShutsDownOnTheTickAfterReparenting()
  {
    Environment.SetEnvironmentVariable(AppConstants.AgentPidEnvironmentVariable, AgentPid.ToString());
    var timeProvider = new FakeTimeProvider();
    var shutdown = new TaskCompletionSource();
    var appLifetime = new Mock<IControlledApplicationLifetime>();
    appLifetime
      .Setup(x => x.Shutdown(0))
      .Callback(() => shutdown.SetResult());
    var processInfo = new FakeProcessInfoLinux(AgentPid);
    var service = CreateService(processInfo, appLifetime, timeProvider);

    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    try
    {
      await service.StartAsync(cts.Token);
      Assert.True(
        await timeProvider.WaitForWaiters(count => count > 0, cancellationToken: cts.Token),
        "Watchdog never started waiting for a tick.");

      processInfo.ParentPid = 1;
      timeProvider.Advance(TimeSpan.FromSeconds(5));

      await shutdown.Task.WaitAsync(cts.Token);
      appLifetime.Verify(x => x.Shutdown(0), Times.Once);
    }
    finally
    {
      Environment.SetEnvironmentVariable(AppConstants.AgentPidEnvironmentVariable, null);
    }
  }

  [Fact]
  public async Task ExecuteAsync_WhenAlreadyReparented_ShutsDownWithoutWaitingForATick()
  {
    Environment.SetEnvironmentVariable(AppConstants.AgentPidEnvironmentVariable, AgentPid.ToString());
    var timeProvider = new FakeTimeProvider();
    var shutdown = new TaskCompletionSource();
    var appLifetime = new Mock<IControlledApplicationLifetime>();
    appLifetime
      .Setup(x => x.Shutdown(0))
      .Callback(() => shutdown.SetResult());
    var processInfo = new FakeProcessInfoLinux(parentPid: 1);
    var service = CreateService(processInfo, appLifetime, timeProvider);

    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    try
    {
      await service.StartAsync(cts.Token);

      // The first check runs before any tick, so an orphaned client notices without
      // any time needing to advance.
      await shutdown.Task.WaitAsync(cts.Token);
      appLifetime.Verify(x => x.Shutdown(0), Times.Once);
      Assert.Equal(1, processInfo.CallCount);
    }
    finally
    {
      Environment.SetEnvironmentVariable(AppConstants.AgentPidEnvironmentVariable, null);
    }
  }

  [Fact]
  public async Task ExecuteAsync_WithInvalidAgentPidEnvVar_DoesNothing()
  {
    Environment.SetEnvironmentVariable(AppConstants.AgentPidEnvironmentVariable, "not-a-pid");
    var timeProvider = new FakeTimeProvider();
    var appLifetime = new Mock<IControlledApplicationLifetime>();
    var processInfo = new FakeProcessInfoLinux(1);
    var service = CreateService(processInfo, appLifetime, timeProvider);

    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    await service.StartAsync(cts.Token);
    await service.StopAsync(cts.Token);

    Assert.Equal(0, processInfo.CallCount);
    appLifetime.Verify(x => x.Shutdown(It.IsAny<int>()), Times.Never);
  }

  [Fact]
  public async Task ExecuteAsync_WithoutAgentPidEnvVar_DoesNothing()
  {
    var timeProvider = new FakeTimeProvider();
    var appLifetime = new Mock<IControlledApplicationLifetime>();
    var processInfo = new FakeProcessInfoLinux(1);
    var service = CreateService(processInfo, appLifetime, timeProvider);

    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    await service.StartAsync(cts.Token);
    await service.StopAsync(cts.Token);

    Assert.Equal(0, processInfo.CallCount);
    appLifetime.Verify(x => x.Shutdown(It.IsAny<int>()), Times.Never);
  }

  private static IHostedService CreateService(
    IProcessInfoLinux processInfo,
    Mock<IControlledApplicationLifetime> appLifetime,
    FakeTimeProvider timeProvider)
  {
    return new AgentLifetimeWatchdog(
      processInfo,
      appLifetime.Object,
      NullLogger<AgentLifetimeWatchdog>.Instance,
      timeProvider);
  }

  private sealed class FakeProcessInfoLinux : IProcessInfoLinux
  {
    public FakeProcessInfoLinux(int parentPid)
    {
      ParentPid = parentPid;
    }

    public int CallCount { get; private set; }
    public int ParentPid { get; set; }

    public int GetParentProcessId()
    {
      CallCount++;
      return ParentPid;
    }
  }
}
