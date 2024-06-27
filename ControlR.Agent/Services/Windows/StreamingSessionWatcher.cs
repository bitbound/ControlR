using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Runtime.Versioning;

namespace ControlR.Agent.Services.Windows;

[SupportedOSPlatform("windows")]
internal class StreamingSessionWatcher(
    IStreamingSessionCache _streamerCache,
    ILogger<StreamingSessionWatcher> _logger) : BackgroundService
{

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(250));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            foreach (var kvp in _streamerCache.Sessions)
            {
                try
                {
                    var session = kvp.Value;

                    if (session.StreamerProcess?.HasExited == true)
                    {
                        _logger.LogInformation("Removing streaming session for process {ProcessId}.", session.StreamerProcess.Id);
                        _ = await _streamerCache.TryRemove(session.StreamerProcess.Id);
                        session.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while checking streamer processes for exit.");
                }
               
            }
        }

        await _streamerCache.KillAllSessions();
    }
}
