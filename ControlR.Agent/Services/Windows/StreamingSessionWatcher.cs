using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Runtime.Versioning;
using System.Timers;
using Timer = System.Timers.Timer;

namespace ControlR.Agent.Services.Windows;

[SupportedOSPlatform("windows")]
internal class StreamingSessionWatcher(
    IStreamingSessionCache streamerCache,
    ILogger<StreamingSessionWatcher> logger) : BackgroundService
{
    private readonly ILogger<StreamingSessionWatcher> _logger = logger;
    private readonly IStreamingSessionCache _cache = streamerCache;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(250));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            foreach (var kvp in _cache.Sessions)
            {
                try
                {
                    var session = kvp.Value;

                    if (session.StreamerProcess?.HasExited == true ||
                        session.WatcherProcess?.HasExited == true)
                    {
                        _logger.LogInformation("Removing streaming session {id}.", session.SessionId);
                        _cache.Sessions.TryRemove(session.SessionId, out _);
                        session.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while checking streamer processes for exit.");
                }
               
            }
        }
    }
}
