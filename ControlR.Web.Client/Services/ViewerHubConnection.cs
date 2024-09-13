using ControlR.Libraries.Clients.Services;
using ControlR.Libraries.Shared.Dtos.StreamerDtos;
using ControlR.Libraries.Shared.Interfaces.HubClients;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.AspNetCore.SignalR.Client;
using MudBlazor;
using System.Runtime.CompilerServices;

namespace ControlR.Web.Client.Services;

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

    Task InvokeCtrlAltDel(string deviceId);

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
    NavigationManager _navMan,
    IServiceProvider _services,
    IBusyCounter _busyCounter,
    IDeviceCache _devicesCache,
    ISettings _settings,
    IDelayer _delayer,
    IMessenger _messenger,
    ILogger<ViewerHubConnection> _logger) : HubConnectionBase(_services, _messenger, _delayer, _logger), IViewerHubConnection, IViewerHubClient
{

    public async Task ClearAlert()
    {
        await TryInvoke(
            async () =>
            {
                await WaitForConnection();
                await Connection.InvokeAsync<Result>(nameof(IViewerHub.ClearAlert));
            });
    }



    public async Task CloseTerminalSession(string deviceId, Guid terminalId)
    {
        await TryInvoke(async () =>
        {
            var request = new CloseTerminalRequestDto(terminalId);
            var wrapper = DtoWrapper.Create(request, DtoType.CloseTerminalRequest);
            await Connection.InvokeAsync(nameof(IViewerHub.SendDtoToAgent), deviceId, wrapper);
        });
    }

    public async Task<Result<TerminalSessionRequestResult>> CreateTerminalSession(string agentConnectionId, Guid terminalId)
    {
        return await TryInvoke(
            async () =>
            {
                Guard.IsNotNull(Connection.ConnectionId);

                var request = new TerminalSessionRequest(terminalId, Connection.ConnectionId);
                return await Connection.InvokeAsync<Result<TerminalSessionRequestResult>>(nameof(IViewerHub.CreateTerminalSession), agentConnectionId, request);
            },
            () => Result.Fail<TerminalSessionRequestResult>("Failed to create terminal session."));
    }

    public async Task<Result<AgentAppSettings>> GetAgentAppSettings(string agentConnectionId)
    {
        return await TryInvoke(
            async () =>
            {
                return await Connection.InvokeAsync<Result<AgentAppSettings>>(nameof(IViewerHub.GetAgentAppSettings), agentConnectionId);
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
                var result = await Connection.InvokeAsync<Result<ServerStatsDto>>(nameof(IViewerHub.GetServerStats));
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
        return await TryInvoke(async () =>
        {
            return await Connection.InvokeAsync<Uri?>(nameof(IViewerHub.GetWebSocketBridgeOrigin));
        },
        () => null);
    }

    public async Task<Result<WindowsSession[]>> GetWindowsSessions(DeviceDto device)
    {
        try
        {
            var sessions = await Connection.InvokeAsync<WindowsSession[]>(nameof(IViewerHub.GetWindowsSessions), device.ConnectionId);
            return Result.Ok(sessions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while getting windows sessions.");
            return Result.Fail<WindowsSession[]>(ex);
        }
    }

    public async Task InvokeCtrlAltDel(string deviceId)
    {
        await TryInvoke(async () =>
        {
            var dto = new InvokeCtrlAltDelRequestDto();
            var dtoWrapper = DtoWrapper.Create(dto, DtoType.InvokeCtrlAltDel);
            await Connection.InvokeAsync(nameof(IViewerHub.SendDtoToAgent), deviceId, dtoWrapper);
        });
    }

    public async Task ReceiveAlertBroadcast(AlertBroadcastDto alert)
    {
        await _messenger.Send(new DtoReceivedMessage<AlertBroadcastDto>(alert));
    }

    public Task ReceiveDeviceUpdate(DeviceDto device)
    {
        _devicesCache.AddOrUpdate(device);
        _messenger.SendGenericMessage(GenericMessageKind.DevicesCacheUpdated);
        return Task.CompletedTask;
    }

    public async Task ReceiveServerStats(ServerStatsDto serverStats)
    {
        var message = new ServerStatsUpdateMessage(serverStats);
        await _messenger.Send(message);
    }


    public Task ReceiveStreamerDownloadProgress(StreamerDownloadProgressDto progressDto)
    {
        _messenger.Send(new StreamerDownloadProgressMessage(progressDto.StreamingSessionId, progressDto.Progress, progressDto.Message));
        return Task.CompletedTask;
    }


    public Task ReceiveTerminalOutput(TerminalOutputDto output)
    {
        _messenger.Send(new TerminalOutputMessage(output));
        return Task.CompletedTask;
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
                _logger.LogDebug(ex, "Failed to reconnect to viewer hub.");
            }
        }
    }

    public async Task RequestDeviceUpdates()
    {
        await TryInvoke(async () =>
        {
            await WaitForConnection();
            var dto = new DeviceUpdateRequestDto();
            var wrapper = DtoWrapper.Create(dto, DtoType.DeviceUpdateRequest);
            await Connection.InvokeAsync(nameof(IViewerHub.SendDtoToUserGroups), wrapper);
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

            var requestDto = new StreamerSessionRequestDto(
                sessionId,
                websocketUri,
                targetSystemSession,
                Connection.ConnectionId,
                agentConnectionId,
                _settings.NotifyUserSessionStart);

            var wrapper = DtoWrapper.Create(requestDto, DtoType.StreamingSessionRequest);
            var result = await Connection.InvokeAsync<Result>(
                nameof(IViewerHub.RequestStreamingSession),
                agentConnectionId,
                wrapper);

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
                var wrapper = DtoWrapper.Create(agentAppSettings, DtoType.SendAppSettings);
                return await Connection.InvokeAsync<Result>(nameof(IViewerHub.SendAgentAppSettings), agentConnectionId, wrapper);
            },
            () => Result.Fail("Failed to send app settings"));
    }

    public async Task SendAgentUpdateTrigger(DeviceDto device)
    {
        await TryInvoke(async () =>
        {
            var dto = new TriggerAgentUpdateDto();
            var wrapper = DtoWrapper.Create(dto, DtoType.TriggerAgentUpdate);
            await Connection.InvokeAsync(nameof(IViewerHub.SendDtoToAgent), device.Id, wrapper);
        });
    }

    public async Task SendAlertBroadcast(string message, AlertSeverity severity)
    {
        await TryInvoke(
            async () =>
            {
                await WaitForConnection();
                var dto = new AlertBroadcastDto(message, severity);
                var wrapper = DtoWrapper.Create(dto, DtoType.SendAlertBroadcast);
                await Connection.InvokeAsync<Result>(nameof(IViewerHub.SendAlertBroadcast), wrapper);
            });
    }


    public async Task SendPowerStateChange(DeviceDto device, PowerStateChangeType powerStateType)
    {
        await TryInvoke(async () =>
        {
            var powerDto = new PowerStateChangeDto(powerStateType);
            var wrapper = DtoWrapper.Create(powerDto, DtoType.PowerStateChange);
            await Connection.InvokeAsync(nameof(IViewerHub.SendDtoToAgent), device.Id, wrapper);
        });
    }

    public async Task<Result> SendTerminalInput(string agentConnectionId, Guid terminalId, string input)
    {
        return await TryInvoke(
            async () =>
            {
                var request = new TerminalInputDto(terminalId, input);
                var wrapper = DtoWrapper.Create(request, DtoType.TerminalInput);
                return await Connection.InvokeAsync<Result>(nameof(IViewerHub.SendTerminalInput), agentConnectionId, wrapper);
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
                await Connection.InvokeAsync(nameof(IViewerHub.SendDtoToUserGroups), wrapper);
            });
    }


    public async Task Start(CancellationToken cancellationToken)
    {
        using var _ = _busyCounter.IncrementBusyCounter();

        await Connect(
            () => new Uri($"{_navMan.BaseUri}hubs/viewer"),
            ConfigureConnection,
            ConfigureHttpOptions,
            OnConnectFailure,
            useReconnect: true,
            cancellationToken);

        await PerformAfterConnectInit();
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
        connection.On<StreamerDownloadProgressDto>(nameof(ReceiveStreamerDownloadProgress), ReceiveStreamerDownloadProgress);
    }

    private void ConfigureHttpOptions(HttpConnectionOptions options)
    {
    }

    private async Task Connection_Closed(Exception? arg)
    {
        await _messenger.Send(new HubConnectionStateChangedMessage(ConnectionState));
    }

    private async Task Connection_Reconnected(string? arg)
    {
        await PerformAfterConnectInit();
    }

    private async Task Connection_Reconnecting(Exception? arg)
    {
        await _messenger.Send(new HubConnectionStateChangedMessage(ConnectionState));
    }

    private async Task OnConnectFailure(string reason)
    {
        await _messenger.Send(new ToastMessage(reason, Severity.Error));
    }

    private async Task PerformAfterConnectInit()
    {
        await GetCurrentAlertFromServer();
        await _devicesCache.SetAllOffline();
        await RequestDeviceUpdates();
        await _messenger.Send(new HubConnectionStateChangedMessage(ConnectionState));
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

    private async Task<T> TryInvoke<T>(Func<Task<T>> func, Func<T> defaultValue, [CallerMemberName] string callerName = "")
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
    private class RetryPolicy : IRetryPolicy
    {
        public TimeSpan? NextRetryDelay(RetryContext retryContext)
        {
            return TimeSpan.FromSeconds(3);
        }
    }
}