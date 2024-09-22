using System.Runtime.CompilerServices;
using ControlR.Libraries.Clients.Services;
using ControlR.Libraries.Shared.Dtos.StreamerDtos;
using ControlR.Libraries.Shared.Interfaces.HubClients;
using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.AspNetCore.SignalR.Client;
using MudBlazor;

namespace ControlR.Viewer.Services;

public interface IViewerHubConnection : IHubConnectionBase
{
  Task ClearAlert();
  Task CloseTerminalSession(string agentConnectionId, Guid terminalId);

  Task<Result<TerminalSessionRequestResult>> CreateTerminalSession(string agentConnectionId, Guid terminalId);

  Task<Result<AgentAppSettings>> GetAgentAppSettings(string agentConnectionId);

  Task<Result<AlertBroadcastDto>> GetCurrentAlertFromServer();

  Task<Result<ServerStatsDto>> GetServerStats();
  Task<Uri?> GetWebsocketBridgeOrigin();
  Task<Result<WindowsSession[]>> GetWindowsSessions(DeviceDto device);

  Task InvokeCtrlAltDel(Guid deviceId);

  Task Reconnect(CancellationToken cancellationToken);

  Task RequestDeviceUpdates();

  Task<Result> RequestStreamingSession(
    string agentConnectionId,
    Guid sessionId,
    Uri websocketUri,
    int targetSystemSession);

  Task<Result> SendAgentAppSettings(string agentConnectionId, AgentAppSettings agentAppSettings);

  Task SendAgentUpdateTrigger(DeviceDto device);

  Task SendAlertBroadcast(string message, AlertSeverity severity);

  Task SendPowerStateChange(DeviceDto device, PowerStateChangeType powerStateType);
  Task<Result> SendTerminalInput(string agentConnectionId, Guid terminalId, string input);
  Task SendWakeDevice(string[] macAddresses);
  Task Start(CancellationToken cancellationToken);
}

internal class ViewerHubConnection(
  IServiceProvider services,
  IHttpConfigurer httpConfigurer,
  IAppState appState,
  IDeviceCache devicesCache,
  IKeyProvider keyProvider,
  ISettings settings,
  IDelayer delayer,
  IMessenger messenger,
  ILogger<ViewerHubConnection> logger) : HubConnectionBase(services, messenger, delayer, logger), IViewerHubConnection,
  IViewerHubClient
{
  public async Task ReceiveAlertBroadcast(AlertBroadcastDto alert)
  {
    await Messenger.Send(new DtoReceivedMessage<AlertBroadcastDto>(alert));
  }

  public Task ReceiveDeviceUpdate(DeviceDto device)
  {
    devicesCache.AddOrUpdate(device);
    Messenger.SendGenericMessage(GenericMessageKind.DevicesCacheUpdated);
    return Task.CompletedTask;
  }

  public async Task ReceiveServerStats(ServerStatsDto serverStats)
  {
    var message = new ServerStatsUpdateMessage(serverStats);
    await Messenger.Send(message);
  }


  public Task ReceiveStreamerDownloadProgress(StreamerDownloadProgressDto progressDto)
  {
    Messenger.Send(new StreamerDownloadProgressMessage(progressDto.StreamingSessionId, progressDto.Progress,
      progressDto.Message));
    return Task.CompletedTask;
  }


  public Task ReceiveTerminalOutput(TerminalOutputDto output)
  {
    Messenger.Send(new TerminalOutputMessage(output));
    return Task.CompletedTask;
  }

  public async Task ClearAlert()
  {
    await TryInvoke(
      async () =>
      {
        await WaitForConnection();
        await Connection.InvokeAsync<Result>(nameof(IViewerHub.ClearAlert));
      });
  }


  public async Task CloseTerminalSession(Guid deviceId, Guid terminalId)
  {
    await TryInvoke(async () =>
    {
      var request = new CloseTerminalRequestDto(terminalId);
      var signedDto = keyProvider.CreateSignedDto(request, DtoType.CloseTerminalRequest, appState.PrivateKey);
      await Connection.InvokeAsync(nameof(IViewerHub.SendSignedDtoToAgent), deviceId, signedDto);
    });
  }

  public async Task<Result<TerminalSessionRequestResult>> CreateTerminalSession(string agentConnectionId,
    Guid terminalId)
  {
    return await TryInvoke(
      async () =>
      {
        Guard.IsNotNull(Connection.ConnectionId);

        var request = new TerminalSessionRequest(terminalId, Connection.ConnectionId);
        var signedDto = keyProvider.CreateSignedDto(request, DtoType.TerminalSessionRequest, appState.PrivateKey);
        return await Connection.InvokeAsync<Result<TerminalSessionRequestResult>>(
          nameof(IViewerHub.CreateTerminalSession), agentConnectionId, signedDto);
      },
      () => Result.Fail<TerminalSessionRequestResult>("Failed to create terminal session."));
  }

  public async Task<Result<AgentAppSettings>> GetAgentAppSettings(string agentConnectionId)
  {
    return await TryInvoke(
      async () =>
      {
        var dto = new GetAgentAppSettingsDto();
        var signedDto = keyProvider.CreateSignedDto(dto, DtoType.GetAgentAppSettings, appState.PrivateKey);
        return await Connection.InvokeAsync<Result<AgentAppSettings>>(nameof(IViewerHub.GetAgentAppSettings),
          agentConnectionId, signedDto);
      },
      () => Result.Fail<AgentAppSettings>("Failed to get agent settings"));
  }

  public async Task<Result<AlertBroadcastDto>> GetCurrentAlertFromServer()
  {
    return await TryInvoke(
      async () =>
      {
        var alertResult = await Connection.InvokeAsync<Result<AlertBroadcastDto>>(nameof(IViewerHub.GetCurrentAlert));
        if (alertResult.IsSuccess)
        {
          await Messenger.Send(new DtoReceivedMessage<AlertBroadcastDto>(alertResult.Value));
        }
        else if (alertResult.HadException)
        {
          alertResult.Log(logger);
        }

        return alertResult;
      },
      () => Result.Fail<AlertBroadcastDto>("Failed to get current alert from the server."));
  }


  public async Task<Result<ServerStatsDto>> GetServerStats()
  {
    return await TryInvoke(
      async () =>
      {
        var result = await Connection.InvokeAsync<Result<ServerStatsDto>>(nameof(IViewerHub.GetServerStats));
        if (!result.IsSuccess)
        {
          logger.LogResult(result);
        }

        return result;
      },
      () => Result.Fail<ServerStatsDto>("Failed to get server stats."));
  }

  public async Task<Uri?> GetWebsocketBridgeOrigin()
  {
    return await TryInvoke(
      async () => { return await Connection.InvokeAsync<Uri?>(nameof(IViewerHub.GetWebSocketBridgeOrigin)); },
      () => null);
  }

  public async Task<Result<WindowsSession[]>> GetWindowsSessions(DeviceDto device)
  {
    try
    {
      var dto = new GetWindowsSessionsDto();
      var signedDto = keyProvider.CreateSignedDto(dto, DtoType.GetWindowsSessions, appState.PrivateKey);
      var sessions =
        await Connection.InvokeAsync<WindowsSession[]>(nameof(IViewerHub.GetWindowsSessions), device.ConnectionId,
          signedDto);
      return Result.Ok(sessions);
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error while getting windows sessions.");
      return Result.Fail<WindowsSession[]>(ex);
    }
  }

  public async Task InvokeCtrlAltDel(Guid deviceId)
  {
    await TryInvoke(async () =>
    {
      var dto = new InvokeCtrlAltDelRequestDto();
      var signedDto = keyProvider.CreateSignedDto(dto, DtoType.InvokeCtrlAltDel, appState.PrivateKey);
      await Connection.InvokeAsync(nameof(IViewerHub.SendSignedDtoToAgent), deviceId, signedDto);
    });
  }

  public async Task Reconnect(CancellationToken cancellationToken)
  {
    while (!cancellationToken.IsCancellationRequested)
    {
      try
      {
        if (IsConnected)
        {
          await Connection.StopAsync(cancellationToken);
        }

        await Start(cancellationToken);
        break;
      }
      catch (Exception ex)
      {
        logger.LogDebug(ex, "Failed to reconnect to viewer hub.");
      }
    }
  }

  public async Task RequestDeviceUpdates()
  {
    await TryInvoke(async () =>
    {
      await WaitForConnection();
      var dto = new DeviceUpdateRequestDto(settings.PublicKeyLabel);
      var signedDto = keyProvider.CreateSignedDto(dto, DtoType.DeviceUpdateRequest, appState.PrivateKey);
      await Connection.InvokeAsync(nameof(IViewerHub.SendSignedDtoToPublicKeyGroup), signedDto);
    });
  }

  public async Task<Result> RequestStreamingSession(
    string agentConnectionId,
    Guid sessionId,
    Uri websocketUri,
    int targetSystemSession)
  {
    try
    {
      if (Connection.ConnectionId is null)
      {
        return Result.Fail("Connection has closed.");
      }

      var streamingSessionRequest = new StreamerSessionRequestDto(
        sessionId,
        websocketUri,
        targetSystemSession,
        Connection.ConnectionId,
        agentConnectionId,
        settings.NotifyUserSessionStart,
        settings.Username);

      var signedDto =
        keyProvider.CreateSignedDto(streamingSessionRequest, DtoType.StreamingSessionRequest, appState.PrivateKey);

      var result = await Connection.InvokeAsync<Result>(
        nameof(IViewerHub.RequestStreamingSession),
        agentConnectionId,
        signedDto);

      return result.Log(logger);
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error while getting remote streaming session.");
      return Result.Fail(ex);
    }
  }

  public async Task<Result> SendAgentAppSettings(string agentConnectionId, AgentAppSettings agentAppSettings)
  {
    return await TryInvoke(
      async () =>
      {
        await WaitForConnection();
        var signedDto = keyProvider.CreateSignedDto(agentAppSettings, DtoType.SendAppSettings, appState.PrivateKey);
        return await Connection.InvokeAsync<Result>(nameof(IViewerHub.SendAgentAppSettings), agentConnectionId,
          signedDto);
      },
      () => Result.Fail("Failed to send app settings"));
  }

  public async Task SendAgentUpdateTrigger(DeviceDto device)
  {
    await TryInvoke(async () =>
    {
      var dto = new TriggerAgentUpdateDto();
      var signedDto = keyProvider.CreateSignedDto(dto, DtoType.TriggerAgentUpdate, appState.PrivateKey);
      await Connection.InvokeAsync(nameof(IViewerHub.SendSignedDtoToAgent), device.Uid, signedDto);
    });
  }

  public async Task SendAlertBroadcast(string message, AlertSeverity severity)
  {
    await TryInvoke(
      async () =>
      {
        await WaitForConnection();
        var dto = new AlertBroadcastDto(message, severity);
        var signedDto = keyProvider.CreateSignedDto(dto, DtoType.SendAlertBroadcast, appState.PrivateKey);
        await Connection.InvokeAsync<Result>(nameof(IViewerHub.SendAlertBroadcast), signedDto);
      });
  }


  public async Task SendPowerStateChange(DeviceDto device, PowerStateChangeType powerStateType)
  {
    await TryInvoke(async () =>
    {
      var powerDto = new PowerStateChangeDto(powerStateType);
      var signedDto = keyProvider.CreateSignedDto(powerDto, DtoType.PowerStateChange, appState.PrivateKey);
      await Connection.InvokeAsync(nameof(IViewerHub.SendSignedDtoToAgent), device.Uid, signedDto);
    });
  }

  public async Task<Result> SendTerminalInput(string agentConnectionId, Guid terminalId, string input)
  {
    return await TryInvoke(
      async () =>
      {
        var request = new TerminalInputDto(terminalId, input);
        var signedDto = keyProvider.CreateSignedDto(request, DtoType.TerminalInput, appState.PrivateKey);
        return await Connection.InvokeAsync<Result>(nameof(IViewerHub.SendTerminalInput), agentConnectionId, signedDto);
      },
      () => Result.Fail("Failed to send terminal input"));
  }

  public async Task SendWakeDevice(string[] macAddresses)
  {
    await TryInvoke(
      async () =>
      {
        var request = new WakeDeviceDto(macAddresses);
        var signedDto = keyProvider.CreateSignedDto(request, DtoType.WakeDevice, appState.PrivateKey);
        await Connection.InvokeAsync(nameof(IViewerHub.SendSignedDtoToPublicKeyGroup), signedDto);
      });
  }


  public async Task Start(CancellationToken cancellationToken)
  {
    Messenger.UnregisterAll(this);

    await Delayer.WaitForAsync(() => appState.IsAuthenticated, TimeSpan.MaxValue);

    using var _ = appState.IncrementBusyCounter();

    await Connect(
      () => new Uri(settings.ServerUri, "/hubs/viewer"),
      ConfigureConnection,
      ConfigureHttpOptions,
      OnConnectFailure,
      true,
      cancellationToken);

    Messenger.RegisterGenericMessage(this, HandleGenericMessage);

    await PerformAfterConnectInit();
  }

  private async Task CheckIfServerAdministrator()
  {
    await TryInvoke(
      async () =>
      {
        appState.IsServerAdministrator =
          await Connection.InvokeAsync<bool>(nameof(IViewerHub.CheckIfServerAdministrator));
        await Messenger.SendGenericMessage(GenericMessageKind.IsServerAdminChanged);
      });
  }


  private void ConfigureConnection(HubConnection connection)
  {
    connection.Closed += Connection_Closed;
    connection.Reconnecting += Connection_Reconnecting;
    connection.Reconnected += Connection_Reconnected;
    connection.On<DeviceDto>(nameof(ReceiveDeviceUpdate), ReceiveDeviceUpdate);
    connection.On<TerminalOutputDto>(nameof(ReceiveTerminalOutput), ReceiveTerminalOutput);
    connection.On<AlertBroadcastDto>(nameof(ReceiveAlertBroadcast), ReceiveAlertBroadcast);
    connection.On<ServerStatsDto>(nameof(ReceiveServerStats), ReceiveServerStats);
    connection.On<StreamerDownloadProgressDto>(nameof(ReceiveStreamerDownloadProgress),
      ReceiveStreamerDownloadProgress);
  }

  private void ConfigureHttpOptions(HttpConnectionOptions options)
  {
    var signature = httpConfigurer.GetDigitalSignature();
    options.Headers["Authorization"] = $"{AuthSchemes.DigitalSignature} {signature}";
  }

  private async Task Connection_Closed(Exception? arg)
  {
    await Messenger.Send(new HubConnectionStateChangedMessage(ConnectionState));
  }

  private async Task Connection_Reconnected(string? arg)
  {
    await PerformAfterConnectInit();
  }

  private async Task Connection_Reconnecting(Exception? arg)
  {
    await Messenger.Send(new HubConnectionStateChangedMessage(ConnectionState));
  }

  private async Task HandleAuthStateChanged()
  {
    await StopConnection(appState.AppExiting);

    if (appState.IsAuthenticated)
    {
      await Start(appState.AppExiting);
    }
  }

  private async Task HandleGenericMessage(object subscriber, GenericMessageKind kind)
  {
    switch (kind)
    {
      case GenericMessageKind.ServerUriChanged:
        await HandleServerUriChanged();
        break;

      case GenericMessageKind.KeysStateChanged:
        await HandleAuthStateChanged();
        break;
    }
  }

  private async Task HandleServerUriChanged()
  {
    await Reconnect(appState.AppExiting);
  }

  private async Task OnConnectFailure(string reason)
  {
    await Messenger.Send(new ToastMessage(reason, Severity.Error));
  }

  private async Task PerformAfterConnectInit()
  {
    await CheckIfServerAdministrator();
    await GetCurrentAlertFromServer();
    await devicesCache.SetAllOffline();
    await RequestDeviceUpdates();
    await Messenger.Send(new HubConnectionStateChangedMessage(ConnectionState));
  }

  private async Task TryInvoke(Func<Task> func, [CallerMemberName] string callerName = "")
  {
    try
    {
      using var _ = logger.BeginScope(callerName);
      await func.Invoke();
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error while invoking hub method.");
    }
  }

  private async Task<T> TryInvoke<T>(Func<Task<T>> func, Func<T> defaultValue,
    [CallerMemberName] string callerName = "")
  {
    try
    {
      using var _ = logger.BeginScope(callerName);
      return await func.Invoke();
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error while invoking hub method.");
      return defaultValue();
    }
  }

  private class RetryPolicy : IRetryPolicy
  {
    public TimeSpan? NextRetryDelay(RetryContext retryContext)
    {
      return TimeSpan.FromSeconds(3);
    }
  }
}