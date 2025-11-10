using ControlR.Agent.Common.Interfaces;
using ControlR.Libraries.Shared.Constants;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.Extensions.Hosting;

namespace ControlR.Agent.Common.Services;

internal class HubConnectionInitializer(
  IHubConnection<IAgentHub> hubConnection,
  IHostApplicationLifetime appLifetime,
  ISettingsProvider settings,
  IDesktopClientUpdater desktopClientUpdater,
  IAgentUpdater agentUpdater,
  IAgentHeartbeatTimer agentHeartbeatTimer,
  ILogger<HubConnectionInitializer> logger) : IHostedService
{
  private readonly IAgentHeartbeatTimer _agentHeartbeatTimer = agentHeartbeatTimer;
  private readonly IAgentUpdater _agentUpdater = agentUpdater;
  private readonly IHostApplicationLifetime _appLifetime = appLifetime;
  private readonly IDesktopClientUpdater _desktopClientUpdater = desktopClientUpdater;
  private readonly IHubConnection<IAgentHub> _hubConnection = hubConnection;
  private readonly ILogger<HubConnectionInitializer> _logger = logger;
  private readonly ISettingsProvider _settings = settings;

  public async Task StartAsync(CancellationToken cancellationToken)
  {
    while (!cancellationToken.IsCancellationRequested)
    {
      try
      {
        if (await Connect(cancellationToken))
        {
          break;
        }
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error while initializing hub connection.");
      }
    }
  }

  public async Task StopAsync(CancellationToken cancellationToken)
  {
    await _hubConnection.DisposeAsync();
  }

  private async Task<bool> Connect(CancellationToken cancellationToken)
  {
    var hubEndpoint = new Uri(_settings.ServerUri, AppConstants.AgentHubPath);

    var result = await _hubConnection.Connect(
      hubEndpoint,
      true,
      options =>
      {
        options.SkipNegotiation = true;
        options.Transports = HttpTransportType.WebSockets;
      },
      cancellationToken);

    if (!result)
    {
      _logger.LogError("Failed to connect to hub.");
      return false;
    }

    await _agentHeartbeatTimer.SendDeviceHeartbeat();

    _hubConnection.Reconnected += HubConnection_Reconnected;
    _hubConnection.Reconnecting += HubConnection_Reconnecting;

    _logger.LogInformation("Connected to hub.");
    return true;
  }

  private async Task HubConnection_Reconnected(string? arg)
  {
    try
    {
      await _agentHeartbeatTimer.SendDeviceHeartbeat();
      await _agentUpdater.CheckForUpdate();
      await _desktopClientUpdater.EnsureLatestVersion(
        true,
        _appLifetime.ApplicationStopping);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while handling hub reconnection.");
    }
  }

  private Task HubConnection_Reconnecting(Exception? arg)
  {
    _logger.LogInformation(arg, "Attempting to reconnect to hub.");
    return Task.CompletedTask;
  }
}