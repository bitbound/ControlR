using System.Runtime.CompilerServices;
using ControlR.Libraries.Clients.Services;
using ControlR.Libraries.Shared.Dtos.StreamerDtos;
using ControlR.Libraries.Shared.Hubs;
using ControlR.Libraries.Shared.Interfaces.HubClients;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.AspNetCore.SignalR.Client;

namespace ControlR.Web.Client.Services;

public interface IViewerHubConnection
{
  HubConnectionState ConnectionState { get; }
  bool IsConnected { get; }
  Task ClearAlert();
  Task CloseTerminalSession(Guid deviceId, Guid terminalId);

  Task Connect(CancellationToken cancellationToken = default);

  Task<Result<TerminalSessionRequestResult>> CreateTerminalSession(string agentConnectionId, Guid terminalId);

  Task<Result<AgentAppSettings>> GetAgentAppSettings(string agentConnectionId);

  Task<Result<AlertBroadcastDto>> GetCurrentAlertFromServer();

  Task<Result<ServerStatsDto>> GetServerStats();
  Task<Uri?> GetWebsocketBridgeOrigin();
  Task<Result<WindowsSession[]>> GetWindowsSessions(DeviceDto device);

  Task InvokeCtrlAltDel(Guid deviceId);

  Task RefreshDevices();

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
}

internal class ViewerHubConnection(
  NavigationManager _navMan,
  IHubConnection<IViewerHub> _viewerHub,
  IBusyCounter _busyCounter,
  IDeviceCache _devicesCache,
  ISettings _settings,
  IMessenger _messenger,
  IDelayer _delayer,
  ILogger<ViewerHubConnection> _logger) : IViewerHubConnection
{
  public HubConnectionState ConnectionState => _viewerHub.ConnectionState;
  public bool IsConnected => _viewerHub.IsConnected;
  public async Task ClearAlert()
  {
    await TryInvoke(
      async () =>
      {
        await WaitForConnection();
        await _viewerHub.Server.ClearAlert();
      });
  }

  public async Task CloseTerminalSession(Guid deviceId, Guid terminalId)
  {
    await TryInvoke(async () =>
    {
      var request = new CloseTerminalRequestDto(terminalId);
      var wrapper = DtoWrapper.Create(request, DtoType.CloseTerminalRequest);
      await _viewerHub.Server.SendDtoToAgent(deviceId, wrapper);
    });
  }

  public async Task Connect(CancellationToken cancellationToken = default)
  {
    using var _ = _busyCounter.IncrementBusyCounter();

    while (true)
    {
      var result = await _viewerHub.Connect(
        hubEndpoint: new Uri($"{_navMan.BaseUri}hubs/viewer"),
        autoRetry: true,
        cancellationToken: cancellationToken);

      if (result)
      {
        break;
      }
    }

    _viewerHub.Closed += Connection_Closed;
    _viewerHub.Reconnecting += Connection_Reconnecting;
    _viewerHub.Reconnected += Connection_Reconnected;
    _viewerHub.ConnectThrew += Connection_Threw;
    await PerformAfterConnectInit();
  }

  public async Task<Result<TerminalSessionRequestResult>> CreateTerminalSession(string agentConnectionId, Guid terminalId)
  {
    return await TryInvoke(
      async () =>
      {
        Guard.IsNotNull(_viewerHub.ConnectionId);

        var request = new TerminalSessionRequest(terminalId, _viewerHub.ConnectionId);

        return await _viewerHub.Server.CreateTerminalSession(agentConnectionId, request);
      },
      () => Result.Fail<TerminalSessionRequestResult>("Failed to create terminal session."));
  }

  public async Task<Result<AgentAppSettings>> GetAgentAppSettings(string agentConnectionId)
  {
    return await TryInvoke(
      async () =>
      {
        return await _viewerHub.Server.GetAgentAppSettings(agentConnectionId);
      },
      () => Result.Fail<AgentAppSettings>("Failed to get agent settings"));
  }

  public async Task<Result<AlertBroadcastDto>> GetCurrentAlertFromServer()
  {
    return await TryInvoke(
      async () =>
      {
        await WaitForConnection();
        var alertResult = await _viewerHub.Server.GetCurrentAlert();
        if (alertResult.IsSuccess)
        {
          await _messenger.Send(new DtoReceivedMessage<AlertBroadcastDto>(alertResult.Value));
        }
        else if (alertResult.HadException)
        {
          alertResult.Log(_logger);
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
        await WaitForConnection();
        var result = await _viewerHub.Server.GetServerStats();
        if (!result.IsSuccess)
        {
          _logger.LogResult(result);
        }

        return result;
      },
      () => Result.Fail<ServerStatsDto>("Failed to get server stats."));
  }

  public async Task<Uri?> GetWebsocketBridgeOrigin()
  {
    return await TryInvoke(
      _viewerHub.Server.GetWebSocketBridgeOrigin,
      () => null);
  }

  public async Task<Result<WindowsSession[]>> GetWindowsSessions(DeviceDto device)
  {
    try
    {
      var sessions = await _viewerHub.Server.GetWindowsSessions(device.ConnectionId);
      return Result.Ok(sessions);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while getting windows sessions.");
      return Result.Fail<WindowsSession[]>(ex);
    }
  }

  public async Task InvokeCtrlAltDel(Guid deviceId)
  {
    await TryInvoke(async () =>
    {
      var dto = new InvokeCtrlAltDelRequestDto();
      var dtoWrapper = DtoWrapper.Create(dto, DtoType.InvokeCtrlAltDel);
      await _viewerHub.Server.SendDtoToAgent(deviceId, dtoWrapper);
    });
  }


  public async Task RefreshDevices()
  {
    await TryInvoke(async () =>
    {
      await WaitForConnection();
      await _devicesCache.SetAllOffline();
      await foreach (var device in _viewerHub.Server.StreamAuthorizedDevices())
      {
        _devicesCache.AddOrUpdate(device);
        await _messenger.SendGenericMessage(GenericMessageKind.DevicesCacheUpdated);
      }
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
      if (_viewerHub.ConnectionId is null)
      {
        return Result.Fail("Connection has closed.");
      }

      var requestDto = new StreamerSessionRequestDto(
        sessionId,
        websocketUri,
        targetSystemSession,
        _viewerHub.ConnectionId,
        agentConnectionId,
        _settings.NotifyUserSessionStart);

      var result = await _viewerHub.Server.RequestStreamingSession(agentConnectionId, requestDto);

      return result.Log(_logger);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while getting remote streaming session.");
      return Result.Fail(ex);
    }
  }

  public async Task<Result> SendAgentAppSettings(string agentConnectionId, AgentAppSettings agentAppSettings)
  {
    return await TryInvoke(
      async () =>
      {
        await WaitForConnection();
        return await _viewerHub.Server.SendAgentAppSettings(agentConnectionId, agentAppSettings);
      },
      () => Result.Fail("Failed to send app settings"));
  }

  public async Task SendAgentUpdateTrigger(DeviceDto device)
  {
    await TryInvoke(async () =>
    {
      var dto = new TriggerAgentUpdateDto();
      var wrapper = DtoWrapper.Create(dto, DtoType.TriggerAgentUpdate);
      await _viewerHub.Server.SendDtoToAgent(device.Id, wrapper);
    });
  }

  public async Task SendAlertBroadcast(string message, AlertSeverity severity)
  {
    await TryInvoke(
      async () =>
      {
        await WaitForConnection();
        var dto = new AlertBroadcastDto(message, severity);
        await _viewerHub.Server.SendAlertBroadcast(dto);
      });
  }


  public async Task SendPowerStateChange(DeviceDto device, PowerStateChangeType powerStateType)
  {
    await TryInvoke(async () =>
    {
      var powerDto = new PowerStateChangeDto(powerStateType);
      var wrapper = DtoWrapper.Create(powerDto, DtoType.PowerStateChange);
      await _viewerHub.Server.SendDtoToAgent(device.Id, wrapper);
    });
  }

  public async Task<Result> SendTerminalInput(string agentConnectionId, Guid terminalId, string input)
  {
    return await TryInvoke(
      async () =>
      {
        var request = new TerminalInputDto(terminalId, input);
        var wrapper = DtoWrapper.Create(request, DtoType.TerminalInput);
        return await _viewerHub.Server.SendTerminalInput(agentConnectionId, request);
      },
      () => Result.Fail("Failed to send terminal input"));
  }

  public async Task SendWakeDevice(string[] macAddresses)
  {
    await TryInvoke(
      async () =>
      {
        var request = new WakeDeviceDto(macAddresses);
        var wrapper = DtoWrapper.Create(request, DtoType.WakeDevice);
        await _viewerHub.Server.SendDtoToUserGroups(wrapper);
      });
  }
  private async Task Connection_Closed(Exception? arg)
  {
    await _messenger.Send(new HubConnectionStateChangedMessage(_viewerHub.ConnectionState));
  }

  private async Task Connection_Reconnected(string? arg)
  {
    await PerformAfterConnectInit();
  }

  private async Task Connection_Reconnecting(Exception? arg)
  {
    await _messenger.Send(new HubConnectionStateChangedMessage(_viewerHub.ConnectionState));
  }

  private async Task Connection_Threw(Exception ex)
  {
    await _messenger.Send(new ToastMessage(ex.Message, Severity.Error));
  }

  private async Task PerformAfterConnectInit()
  {
    await GetCurrentAlertFromServer();
    await RefreshDevices();
    await _messenger.Send(new HubConnectionStateChangedMessage(_viewerHub.ConnectionState));
  }

  private async Task TryInvoke(Func<Task> func, [CallerMemberName] string callerName = "")
  {
    try
    {
      using var _ = _logger.BeginScope(callerName);
      await func.Invoke();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while invoking hub method.");
    }
  }

  private async Task<T> TryInvoke<T>(Func<Task<T>> func, Func<T> defaultValue,
    [CallerMemberName] string callerName = "")
  {
    try
    {
      using var _ = _logger.BeginScope(callerName);
      return await func.Invoke();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while invoking hub method.");
      return defaultValue();
    }
  }

  private async Task WaitForConnection()
  {
    await _delayer.WaitForAsync(() => _viewerHub.IsConnected);
  }

  private class RetryPolicy : IRetryPolicy
  {
    public TimeSpan? NextRetryDelay(RetryContext retryContext)
    {
      return TimeSpan.FromSeconds(3);
    }
  }
}