using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Runtime.Versioning;
using System.Timers;
using Timer = System.Timers.Timer;

namespace ControlR.Agent.Services.Windows;

[SupportedOSPlatform("windows")]
internal class StreamingSessionWatcher(
    IStreamingSessionCache streamerCache,
    ILogger<StreamingSessionWatcher> logger) : IHostedService
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly ILogger<StreamingSessionWatcher> _logger = logger;
    private readonly Timer _timer = new(50);
    private readonly IStreamingSessionCache _cache = streamerCache;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _timer.Elapsed += Timer_Elapsed;
        _timer.Start();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer.Dispose();
        return Task.CompletedTask;
    }


    private async void Timer_Elapsed(object? sender, ElapsedEventArgs e)
    {
        if (!await _lock.WaitAsync(0))
        {
            return;
        }

        try
        {
            foreach (var kvp in _cache.Sessions)
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
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while checking streamer processes.");
        }
        finally
        {
            _lock.Release();
        }
    }
}
