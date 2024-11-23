using ControlR.Agent.Common.Interfaces;
using ControlR.Agent.Common.Models;
using ControlR.Libraries.Shared.Dtos.ServerApi;
using ControlR.Libraries.Shared.Dtos.StreamerDtos;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Hosting;

namespace ControlR.Agent.Common.Services;

public interface IAgentHubConnection : IAsyncDisposable
{
  HubConnectionState State { get; }
  Task Connect(CancellationToken cancellationToken);
  Task SendDeviceHeartbeat();
  Task SendStreamerDownloadProgress(StreamerDownloadProgressDto progressDto);
  Task SendTerminalOutputToViewer(string viewerConnectionId, TerminalOutputDto outputDto);
}

internal class AgentHubConnection(
  IHubConnection<IAgentHub> hubConnection,
  IHostApplicationLifetime appLifetime,
  IDeviceDataGenerator deviceCreator,
  ISettingsProvider settings,
  ICpuUtilizationSampler cpuSampler,
  IStreamerUpdater streamerUpdater,
  IAgentUpdater agentUpdater,
  ILogger<AgentHubConnection> logger)
  : IAgentHubConnection
{
  private readonly IAgentUpdater _agentUpdater = agentUpdater;
  private readonly IHostApplicationLifetime _appLifetime = appLifetime;
  private readonly ICpuUtilizationSampler _cpuSampler = cpuSampler;
  private readonly IDeviceDataGenerator _deviceCreator = deviceCreator;
  private readonly IHubConnection<IAgentHub> _hubConnection = hubConnection;
  private readonly ILogger<AgentHubConnection> _logger = logger;
  private readonly ISettingsProvider _settings = settings;
  private readonly IStreamerUpdater _streamerUpdater = streamerUpdater;
  public HubConnectionState State => _hubConnection.ConnectionState;

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

      var device = await _deviceCreator.CreateDevice(
        _cpuSampler.CurrentUtilization,
        _settings.DeviceId);

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
        await _settings.UpdateId(updateResult.Value.Id);
      }

      await _settings.ClearTags();
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
}