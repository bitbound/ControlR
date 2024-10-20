using ControlR.Libraries.Agent.Interfaces;
using ControlR.Libraries.Shared.Dtos.StreamerDtos;
using ControlR.Libraries.Shared.Extensions;
using ControlR.Libraries.Shared.Hubs;
using ControlR.Libraries.Signalr.Client;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Hosting;

namespace ControlR.Libraries.Agent.Services;

public interface IAgentHubConnection : IAsyncDisposable
{
  Task Connect(CancellationToken cancellationToken);
  Task SendDeviceHeartbeat();
  Task SendStreamerDownloadProgress(StreamerDownloadProgressDto progressDto);
  Task SendTerminalOutputToViewer(string viewerConnectionId, TerminalOutputDto outputDto);
}

internal class AgentHubConnection(
  IHubConnection<IAgentHub> _hubConnection,
  IHostApplicationLifetime _appLifetime,
  IDeviceDataGenerator _deviceCreator,
  ISettingsProvider _settings,
  ICpuUtilizationSampler _cpuSampler,
  IStreamerUpdater _streamerUpdater,
  IAgentUpdater _agentUpdater,
  ILogger<AgentHubConnection> _logger)
  : IAgentHubConnection
{
  public async Task Connect(CancellationToken cancellationToken)
  {
    var hubEndpoint = new Uri(_settings.ServerUri, "/hubs/agent");

    var result = await _hubConnection.Connect(
      hubEndpoint,
      true,
      options =>
      {
        options.SkipNegotiation = true;
        options.Transports = HttpTransportType.WebSockets;
      },
      _appLifetime.ApplicationStopping);

    if (!result)
    {
      _logger.LogError("Failed to connect to hub.");
      return;
    }

    await SendDeviceHeartbeat();

    _hubConnection.Reconnected += HubConnection_Reconnected;

    _logger.LogInformation("Connected to hub.");
  }

  public async ValueTask DisposeAsync()
  {
    await _hubConnection.DisposeAsync();
  }

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

      var deviceDto = await _deviceCreator.CreateDevice(
        _cpuSampler.CurrentUtilization,
        _settings.DeviceId);

      var updateResult = await _hubConnection.Server.UpdateDevice(deviceDto);

      if (!updateResult.IsSuccess)
      {
        _logger.LogResult(updateResult);
        return;
      }

      if (updateResult.Value.Id != deviceDto.Id)
      {
        _logger.LogInformation("Device ID changed.  Updating appsettings.");
        await _settings.UpdateId(updateResult.Value.Id);
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while sending device update.");
    }
  }

  public async Task SendStreamerDownloadProgress(StreamerDownloadProgressDto progressDto)
  {
    await _hubConnection.Server.SendStreamerDownloadProgress(progressDto);
  }

  public async Task SendTerminalOutputToViewer(string viewerConnectionId, TerminalOutputDto outputDto)
  {
    try
    {
      await _hubConnection.Server.SendTerminalOutputToViewer(viewerConnectionId, outputDto);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while sending output to viewer.");
    }
  }
  private async Task HubConnection_Reconnected(string? arg)
  {
    await SendDeviceHeartbeat();
    await _agentUpdater.CheckForUpdate();
    await _streamerUpdater.EnsureLatestVersion(_appLifetime.ApplicationStopping);
  }


  private class RetryPolicy : IRetryPolicy
  {
    public TimeSpan? NextRetryDelay(RetryContext retryContext)
    {
      var waitSeconds = Math.Min(30, Math.Pow(retryContext.PreviousRetryCount, 2));
      return TimeSpan.FromSeconds(waitSeconds);
    }
  }
}