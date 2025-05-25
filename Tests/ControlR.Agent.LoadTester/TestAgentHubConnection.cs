using Bitbound.SimpleMessenger;
using ControlR.Agent.Common.Interfaces;
using ControlR.Agent.Common.Models;
using ControlR.Agent.Common.Services;
using ControlR.Libraries.Clients.Services;
using ControlR.Libraries.Shared.Dtos.HubDtos;
using ControlR.Libraries.Shared.Dtos.ServerApi;
using ControlR.Libraries.Shared.Dtos.StreamerDtos;
using ControlR.Libraries.Shared.Extensions;
using ControlR.Libraries.Shared.Hubs;
using ControlR.Libraries.Shared.Interfaces.HubClients;
using ControlR.Libraries.Shared.Primitives;
using ControlR.Libraries.Shared.Services;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ControlR.Agent.LoadTester;

public class TestAgentHubConnection(
  IServiceProvider services,
  IMessenger messenger,
  IDelayer delayer,
  ISettingsProvider settings,
  IHubConnectionConfigurer hubConnectionConfigurer,
  IHostApplicationLifetime appLifetime,
  IDeviceDataGenerator deviceCreator,
  IAgentHubClient agentHubClient,
  ILogger<HubConnectionBase> logger)
  : HubConnectionBase(services, messenger, delayer, logger), IAgentHubConnection
{

  private readonly ISettingsProvider _settings = settings;
  private readonly IHubConnectionConfigurer _hubConnectionConfigurer = hubConnectionConfigurer;
  private readonly IHostApplicationLifetime _appLifetime = appLifetime;
  private readonly IDeviceDataGenerator _deviceCreator = deviceCreator;
  private readonly IAgentHubClient _agentHubClient = agentHubClient;
  public HubConnectionState State => Connection.State;

  public async Task Connect(CancellationToken cancellationToken)
  {
    var hubEndpoint = new Uri(_settings.ServerUri, "/hubs/agent");

    await Connect(
      () => hubEndpoint,
      ConfigureConnection,
      _hubConnectionConfigurer.ConfigureHubConnection,
      true,
      _appLifetime.ApplicationStopping);

    await SendDeviceHeartbeat();

    Logger.LogInformation("Connected to hub.");
  }

  private void ConfigureConnection(HubConnection connection)
  {
    connection.On<StreamerSessionRequestDto, bool>(
      nameof(IAgentHubClient.CreateStreamingSession),
      _agentHubClient.CreateStreamingSession);

    connection.On<TerminalSessionRequest, Result<TerminalSessionRequestResult>>(
      nameof(IAgentHubClient.CreateTerminalSession),
      _agentHubClient.CreateTerminalSession);

    connection.On(
      nameof(IAgentHubClient.GetWindowsSessions),
       _agentHubClient.GetWindowsSessions);

    connection.On<TerminalInputDto, Result>(
      nameof(IAgentHubClient.ReceiveTerminalInput),
      _agentHubClient.ReceiveTerminalInput);

    connection.On<string>(nameof(IAgentHubClient.UninstallAgent), s => 
    {
      Logger.LogInformation("Received request to uninstall agent: {Reason}", s);
    });
  }


  public ValueTask DisposeAsync()
  {
    throw new NotImplementedException();
  }

  public async Task SendDeviceHeartbeat()
  {
    try
    {

      if (ConnectionState != HubConnectionState.Connected)
      {
        Logger.LogWarning("Not connected to hub when trying to send device update.");
        return;
      }

      var device = await _deviceCreator.CreateDevice(_settings.DeviceId);

      var dto = device.CloneAs<DeviceModel, DeviceDto>();

      var updateResult = await Connection.InvokeAsync<Result<DeviceDto>>(nameof(IAgentHub.UpdateDevice), dto);

      if (!updateResult.IsSuccess)
      {
        Logger.LogResult(updateResult);
        return;
      }

      if (updateResult.Value.Id != device.Id)
      {
        Logger.LogInformation("Device ID changed.  Updating appsettings.");
        await _settings.UpdateId(updateResult.Value.Id);
      }
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while sending device update.");
    }
  }

  public Task SendStreamerDownloadProgress(StreamerDownloadProgressDto progressDto)
  {
    throw new NotImplementedException();
  }

  public async Task SendTerminalOutputToViewer(string viewerConnectionId, TerminalOutputDto outputDto)
  {
      try
    {
      await Connection.InvokeAsync(nameof(IAgentHub.SendTerminalOutputToViewer), viewerConnectionId, outputDto);
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while sending output to viewer.");
    }
  }
}