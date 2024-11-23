using Microsoft.Extensions.Hosting;

namespace ControlR.Agent.Common.Services;

internal class AgentHeartbeatTimer(
    IAgentHubConnection hubConnection,
    ISystemEnvironment systemEnvironment,
    ILogger<AgentHeartbeatTimer> logger) : BackgroundService
{
  private readonly IAgentHubConnection _hubConnection = hubConnection;
  private readonly ILogger<AgentHeartbeatTimer> _logger = logger;
  private readonly ISystemEnvironment _systemEnvironment = systemEnvironment;

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    var delayTime = _systemEnvironment.IsDebug ?
        TimeSpan.FromSeconds(10) :
        TimeSpan.FromMinutes(5);

    using var timer = new PeriodicTimer(delayTime);
    try
    {
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
    catch (OperationCanceledException)
    {
      _logger.LogInformation("Heartbeat aborted.  Application shutting down.");
    }
  }
}
