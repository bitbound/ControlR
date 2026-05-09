using System.Security.Cryptography;
using ControlR.Agent.Shared.Services;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.Extensions.Hosting;

namespace ControlR.Agent.Common.Services;

internal class HubConnectionInitializer(
  TimeProvider timeProvider,
  IHubConnection<IAgentHub> hubConnection,
  IHostApplicationLifetime appLifetime,
  IOptionsAccessor optionsAccessor,
  IAgentMaintenanceService agentUpdater,
  IAgentHeartbeatTimer agentHeartbeatTimer,
  ILogger<HubConnectionInitializer> logger) : IHostedService
{
  private readonly IAgentHeartbeatTimer _agentHeartbeatTimer = agentHeartbeatTimer;
  private readonly IAgentMaintenanceService _agentUpdater = agentUpdater;
  private readonly IHostApplicationLifetime _appLifetime = appLifetime;
  private readonly IHubConnection<IAgentHub> _hubConnection = hubConnection;
  private readonly ILogger<HubConnectionInitializer> _logger = logger;
  private readonly TimeSpan _maxReconnectDelay = TimeSpan.FromSeconds(180);
  private readonly TimeSpan _maxReconnectJitter = TimeSpan.FromSeconds(20);
  private readonly IOptionsAccessor _optionsAccessor = optionsAccessor;
  private readonly TimeProvider _timeProvider = timeProvider;

  public async Task StartAsync(CancellationToken cancellationToken)
  {
    var attempt = 1;
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

      try
      {
        var delay = GetNextRetryDelay(attempt);
        _logger.LogInformation("Waiting {delay} before next connection attempt.", delay);
        await Task
          .Delay(delay, _timeProvider, cancellationToken)
          .IgnoreOperationCanceledException();
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error while waiting before next connection attempt.");
      }
    }
  }

  public async Task StopAsync(CancellationToken cancellationToken)
  {
    await _hubConnection.DisposeAsync();
  }

  private async Task<bool> Connect(CancellationToken cancellationToken)
  {
    var hubEndpoint = new Uri(_optionsAccessor.ServerUri, AppConstants.AgentHubPath);

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

  private TimeSpan GetNextRetryDelay(long retryCount)
  {
    var waitSeconds = Math.Min(Math.Pow(retryCount, 2), _maxReconnectDelay.TotalSeconds);
    var jitterMs = RandomNumberGenerator.GetInt32(0, (int)_maxReconnectJitter.TotalMilliseconds);
    var waitTime = TimeSpan.FromSeconds(waitSeconds) + TimeSpan.FromMilliseconds(jitterMs);
    return waitTime;
  }

  private async Task HubConnection_Reconnected(string? arg)
  {
    try
    {
      using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(30));
      using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
        cts.Token,
        _appLifetime.ApplicationStopping);

      await _agentHeartbeatTimer.SendDeviceHeartbeat();
      await _agentUpdater.CheckForUpdate(cancellationToken: linkedCts.Token);
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