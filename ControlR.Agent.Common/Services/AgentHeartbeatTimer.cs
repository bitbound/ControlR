using ControlR.Agent.Common.Interfaces;
using ControlR.Agent.Common.Models;
using ControlR.Libraries.Shared.Dtos.ServerApi;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Hosting;

namespace ControlR.Agent.Common.Services;

internal interface IAgentHeartbeatTimer : IHostedService
{
  Task SendDeviceHeartbeat();
}

internal class AgentHeartbeatTimer(
  TimeProvider timeProvider,
  IHubConnection<IAgentHub> hubConnection,
  ISystemEnvironment systemEnvironment,
  IDeviceDataGenerator deviceDataGenerator,
  ISettingsProvider settingsProvider,
  ILogger<AgentHeartbeatTimer> logger) : BackgroundService, IAgentHeartbeatTimer
{
  private readonly IDeviceDataGenerator _deviceDataGenerator = deviceDataGenerator;
  private readonly IHubConnection<IAgentHub> _hubConnection = hubConnection;
  private readonly ILogger<AgentHeartbeatTimer> _logger = logger;
  private readonly ISettingsProvider _settingsProvider = settingsProvider;
  private readonly ISystemEnvironment _systemEnvironment = systemEnvironment;
  private readonly TimeProvider _timeProvider = timeProvider;
  
  public async Task SendDeviceHeartbeat()
  {
    try
    {
      using var _ = _logger.BeginMemberScope();

      if (_hubConnection.ConnectionState != HubConnectionState.Connected)
      {
        _logger.LogWarning("Not connected to hub when trying to send device update.");
        return;
      }

      var device = await _deviceDataGenerator.CreateDevice(_settingsProvider.DeviceId);

      var dto = device.CloneAs<DeviceModel, DeviceDto>();

      var updateResult = await _hubConnection.Server.UpdateDevice(dto);

      if (!updateResult.IsSuccess)
      {
        _logger.LogResult(updateResult);
        return;
      }

      if (updateResult.Value.Id != device.Id)
      {
        _logger.LogInformation("Device ID changed.  Updating appsettings.");
        await _settingsProvider.UpdateId(updateResult.Value.Id);
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while sending device update.");
    }
  }


  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    var delayTime = _systemEnvironment.IsDebug ?
        TimeSpan.FromSeconds(10) :
        TimeSpan.FromMinutes(5);

    using var timer = new PeriodicTimer(delayTime, _timeProvider);
    try
    {
      while (await timer.WaitForNextTickAsync(stoppingToken))
      {
        try
        {
          await SendDeviceHeartbeat();
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
