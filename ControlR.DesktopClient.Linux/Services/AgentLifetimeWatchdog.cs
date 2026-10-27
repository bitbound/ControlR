using Avalonia.Controls.ApplicationLifetimes;
using ControlR.Libraries.NativeInterop.Linux;
using System.Runtime.Versioning;
using ControlR.Libraries.Shared.Constants;
using ControlR.Libraries.Shared.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ControlR.DesktopClient.Linux.Services;

/// <summary>
/// Shuts this client down once the agent that spawned it is gone. On Linux a child outlives its
/// parent, so a greeter client whose agent updated, crashed, or was killed keeps holding the greeter
/// display while nothing can control it. The agent names itself in the environment when it spawns this
/// client, and a client whose agent no longer exists has no purpose left.
/// </summary>
/// <remarks>
/// Only the agent's greeter launch sets the variable. A user session client is started by the user's
/// systemd instance and is supervised there, so it must not treat systemd as a parent it can lose.
/// <para>
/// The parent either still matches the pid the agent named or it never will again. A process whose
/// parent exits is reparented once and is not adopted back, so a mismatch is permanent rather than a
/// transient to wait out.
/// </para>
/// </remarks>
[SupportedOSPlatform("linux")]
internal sealed class AgentLifetimeWatchdog(
  IProcessInfoLinux processInfo,
  IControlledApplicationLifetime appLifetime,
  ILogger<AgentLifetimeWatchdog> logger,
  TimeProvider timeProvider) : BackgroundService
{
  private static readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(5);

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    if (!int.TryParse(
          Environment.GetEnvironmentVariable(AppConstants.AgentPidEnvironmentVariable),
          out var agentPid) ||
        agentPid <= 0)
    {
      return;
    }

    using var timer = new PeriodicTimer(_pollInterval, timeProvider);

    do
    {
      var parentPid = processInfo.GetParentProcessId();
      if (parentPid == agentPid)
      {
        continue;
      }

      logger.LogInformation(
        "The agent that started this desktop client, PID {AgentPid}, is gone. This process was reparented to PID {ParentPid}. Shutting down.",
        agentPid,
        parentPid);
      appLifetime.Shutdown(0);
      return;
    }
    while (await timer.WaitForNextTick(throwOnCancellation: false, stoppingToken));
  }
}
