using System.Runtime.Versioning;
using Microsoft.Extensions.Hosting;

namespace ControlR.Agent.Services.Windows;

[SupportedOSPlatform("windows")]
internal class StreamingSessionWatcher(
  IStreamingSessionCache streamerCache,
  ILogger<StreamingSessionWatcher> logger) : BackgroundService
{
  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(250));

    while (await timer.WaitForNextTickAsync(stoppingToken))
    {
      foreach (var kvp in streamerCache.Sessions)
      {
        try
        {
          var session = kvp.Value;

          if (session.StreamerProcess?.HasExited == true)
          {
            logger.LogInformation("Removing streaming session for process {ProcessId}.", session.StreamerProcess.Id);
            _ = await streamerCache.TryRemove(session.StreamerProcess.Id);
            session.Dispose();
          }
        }
        catch (Exception ex)
        {
          logger.LogError(ex, "Error while checking streamer processes for exit.");
        }
      }
    }

    await streamerCache.KillAllSessions();
  }
}