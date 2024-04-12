using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ControlR.Agent.Services;
internal class AgentHeartbeatTimer(
    IAgentHubConnection hubConnection,
    ILogger<AgentHeartbeatTimer> logger) : BackgroundService
{
    private readonly IAgentHubConnection _hubConnection = hubConnection;
    private readonly ILogger<AgentHeartbeatTimer> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await _hubConnection.SendDeviceHeartbeat();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while sending agent heartbeat.");
            }
        }
    }
}
