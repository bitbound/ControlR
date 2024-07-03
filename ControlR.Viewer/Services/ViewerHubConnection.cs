using ControlR.Libraries.Shared.Dtos.SidecarDtos;
using ControlR.Libraries.Shared.Dtos.StreamerDtos;
using ControlR.Libraries.Shared.Interfaces.HubClients;
using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.AspNetCore.SignalR.Client;
using MudBlazor;
using System.Runtime.CompilerServices;

namespace ControlR.Viewer.Services;

public interface IViewerHubConnection : IHubConnectionBase
{
    Task ClearAlert();

    Task CloseStreamingSession(string streamerConnectionId);

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
    Task SendChangeDisplaysRequest(string streamerConnectionId, string displayId);
    Task SendClipboardText(string streamerConnectionId, string text, Guid sessionId);
    Task SendKeyboardStateReset(string streamerConnectionId);
    Task SendKeyEvent(string streamerConnectionId, string key, bool isPressed);
    Task SendMouseButtonEvent(string streamerConnectionId, int button, bool isPressed, double percentX, double percentY);
    Task SendPointerMove(string streamerConnectionId, double percentX, double percentY);
    Task SendPowerStateChange(DeviceDto device, PowerStateChangeType powerStateType);
    Task SendReadySignalToStreamer(string streamerConnectionId);
    Task<Result> SendTerminalInput(string agentConnectionId, Guid terminalId, string input);
    Task SendTypeText(string streamerConnectionId, string text);
    Task SendWakeDevice(string[] macAddresses);
    Task SendWheelScroll(string streamerConnectionId, double percentX, double percentY, double scrollY, double scrollX);
    Task Start(CancellationToken cancellationToken);
}

internal class ViewerHubConnection(
    IServiceProvider _services,
    IHttpConfigurer _httpConfigurer,
    IAppState _appState,
    IDeviceCache _devicesCache,
    IKeyProvider _keyProvider,
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
                var signedDto = _keyProvider.CreateRandomSignedDto(DtoType.ClearAlerts, _appState.PrivateKey);
                await Connection.InvokeAsync<Result>(nameof(IViewerHub.ClearAlert), signedDto);
            });
    }

    public async Task CloseStreamingSession(string streamerConnectionId)
    {
        await TryInvoke(
            async () =>
            {
                var signedDto = _keyProvider.CreateRandomSignedDto(DtoType.CloseStreamingSession, _appState.PrivateKey);
                await Connection.InvokeAsync(nameof(IViewerHub.SendSignedDtoToStreamer), streamerConnectionId, signedDto);
            });
    }

    public async Task CloseTerminalSession(string deviceId, Guid terminalId)
    {
        await TryInvoke(async () =>
        {
            var request = new CloseTerminalRequestDto(terminalId);
            var signedDto = _keyProvider.CreateSignedDto(request, DtoType.CloseTerminalRequest, _appState.PrivateKey);
            await Connection.InvokeAsync(nameof(IViewerHub.SendSignedDtoToAgent), deviceId, signedDto);
        });
    }

    public async Task<Result<TerminalSessionRequestResult>> CreateTerminalSession(string agentConnectionId, Guid terminalId)
    {
        return await TryInvoke(
            async () =>
            {
                Guard.IsNotNull(Connection.ConnectionId);

                var request = new TerminalSessionRequest(terminalId, Connection.ConnectionId);
                var signedDto = _keyProvider.CreateSignedDto(request, DtoType.TerminalSessionRequest, _appState.PrivateKey);
                return await Connection.InvokeAsync<Result<TerminalSessionRequestResult>>(nameof(IViewerHub.CreateTerminalSession), agentConnectionId, signedDto);
            },
            () => Result.Fail<TerminalSessionRequestResult>("Failed to create terminal session."));
    }

    public async Task<Result<AgentAppSettings>> GetAgentAppSettings(string agentConnectionId)
    {
        return await TryInvoke(
            async () =>
            {
                var request = _keyProvider.CreateRandomSignedDto(DtoType.GetAgentAppSettings, _appState.PrivateKey);
                var signedDto = _keyProvider.CreateSignedDto(request, DtoType.TerminalSessionRequest, _appState.PrivateKey);
                return await Connection.InvokeAsync<Result<AgentAppSettings>>(nameof(IViewerHub.GetAgentAppSettings), agentConnectionId, signedDto);
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
            var signedDto = _keyProvider.CreateRandomSignedDto(DtoType.WindowsSessions, _appState.PrivateKey);
            var sessions = await Connection.InvokeAsync<WindowsSession[]>(nameof(IViewerHub.GetWindowsSessions), device.ConnectionId, signedDto);
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
            var signedDto = _keyProvider.CreateRandomSignedDto(DtoType.InvokeCtrlAltDel, _appState.PrivateKey);
            await Connection.InvokeAsync(nameof(IViewerHub.SendSignedDtoToAgent), deviceId, signedDto);
        });
    }

    public async Task ReceiveAlertBroadcast(AlertBroadcastDto alert)
    {
        await _messenger.Send(new DtoReceivedMessage<AlertBroadcastDto>(alert));
    }

    public async Task ReceiveClipboardChanged(ClipboardChangeDto clipboardChangeDto)
    {
        await _messenger.Send(new DtoReceivedMessage<ClipboardChangeDto>(clipboardChangeDto));
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

    public async Task ReceiveStreamerDisconnected(Guid sessionId)
    {
        await _messenger.Send(new StreamerDisconnectedMessage(sessionId));
    }

    public Task ReceiveStreamerDownloadProgress(StreamerDownloadProgressDto progressDto)
    {
        _messenger.Send(new StreamerDownloadProgressMessage(progressDto.StreamingSessionId, progressDto.Progress, progressDto.Message));
        return Task.CompletedTask;
    }

    public Task ReceiveStreamerInitData(StreamerInitDataDto streamerInitData)
    {
        _messenger.Send(new StreamerInitDataReceivedMessage(streamerInitData));
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
            var dto = new DeviceUpdateRequestDto(_settings.PublicKeyLabel);
            var signedDto = _keyProvider.CreateSignedDto(dto, DtoType.DeviceUpdateRequest, _appState.PrivateKey);
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
                _settings.NotifyUserSessionStart,
                _settings.Username);

            var signedDto = _keyProvider.CreateSignedDto(streamingSessionRequest, DtoType.StreamingSessionRequest, _appState.PrivateKey);

            var result = await Connection.InvokeAsync<Result>(
                nameof(IViewerHub.RequestStreamingSession),
                agentConnectionId,
                signedDto);

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
                var signedDto = _keyProvider.CreateSignedDto(agentAppSettings, DtoType.SendAppSettings, _appState.PrivateKey);
                return await Connection.InvokeAsync<Result>(nameof(IViewerHub.SendAgentAppSettings), agentConnectionId, signedDto);
            },
            () => Result.Fail("Failed to send app settings"));
    }

    public async Task SendAgentUpdateTrigger(DeviceDto device)
    {
        await TryInvoke(async () =>
        {
            var signedDto = _keyProvider.CreateRandomSignedDto(DtoType.AgentUpdateTrigger, _appState.PrivateKey);
            await Connection.InvokeAsync(nameof(IViewerHub.SendSignedDtoToAgent), device.Id, signedDto);
        });
    }

    public async Task SendAlertBroadcast(string message, AlertSeverity severity)
    {
        await TryInvoke(
            async () =>
            {
                await WaitForConnection();
                var dto = new AlertBroadcastDto(message, severity);
                var signedDto = _keyProvider.CreateSignedDto(dto, DtoType.SendAlertBroadcast, _appState.PrivateKey);
                await Connection.InvokeAsync<Result>(nameof(IViewerHub.SendAlertBroadcast), signedDto);
            });
    }

    public async Task SendChangeDisplaysRequest(string streamerConnectionId, string displayId)
    {
        await TryInvoke(
            async () =>
            {
                await WaitForConnection();
                var dto = new ChangeDisplaysDto(displayId);
                var signedDto = _keyProvider.CreateSignedDto(dto, DtoType.ChangeDisplays, _appState.PrivateKey);
                await Connection.InvokeAsync(nameof(IViewerHub.SendSignedDtoToStreamer), streamerConnectionId, signedDto);
            });
    }

    public async Task SendClipboardText(string streamerConnectionId, string text, Guid sessionId)
    {
        await TryInvoke(
             async () =>
             {
                 await WaitForConnection();
                 var dto = new ClipboardChangeDto(text, sessionId);
                 var signedDto = _keyProvider.CreateSignedDto(dto, DtoType.ClipboardChanged, _appState.PrivateKey);
                 await Connection.InvokeAsync(nameof(IViewerHub.SendSignedDtoToStreamer), streamerConnectionId, signedDto);
             });
    }

    public async Task SendKeyboardStateReset(string streamerConnectionId)
    {
        await TryInvoke(
              async () =>
              {
                  await WaitForConnection();
                  var signedDto = _keyProvider.CreateRandomSignedDto(DtoType.ResetKeyboardState, _appState.PrivateKey);
                  await Connection.InvokeAsync(nameof(IViewerHub.SendSignedDtoToStreamer), streamerConnectionId, signedDto);
              });
    }

    public async Task SendKeyEvent(string streamerConnectionId, string key, bool isPressed)
    {
        await TryInvoke(
              async () =>
              {
                  await WaitForConnection();
                  var dto = new KeyEventDto(key, isPressed);
                  var signedDto = _keyProvider.CreateSignedDto(dto, DtoType.KeyEvent, _appState.PrivateKey);
                  await Connection.SendAsync(nameof(IViewerHub.SendSignedDtoToStreamer), streamerConnectionId, signedDto);
              });
    }

    public async Task SendMouseButtonEvent(string streamerConnectionId, int button, bool isPressed, double percentX, double percentY)
    {
        await TryInvoke(
                   async () =>
                   {
                       await WaitForConnection();
                       var dto = new MouseButtonEventDto(button, isPressed, percentX, percentY);
                       var signedDto = _keyProvider.CreateSignedDto(dto, DtoType.MouseButtonEvent, _appState.PrivateKey);
                       await Connection.SendAsync(nameof(IViewerHub.SendSignedDtoToStreamer), streamerConnectionId, signedDto);
                   });
    }

    public async Task SendPointerMove(string streamerConnectionId, double percentX, double percentY)
    {
        await TryInvoke(
                  async () =>
                  {
                      await WaitForConnection();
                      var dto = new MovePointerDto(percentX, percentY);
                      var signedDto = _keyProvider.CreateSignedDto(dto, DtoType.MovePointer, _appState.PrivateKey);
                      await Connection.SendAsync(nameof(IViewerHub.SendSignedDtoToStreamer), streamerConnectionId, signedDto);
                  });
    }

    public async Task SendPowerStateChange(DeviceDto device, PowerStateChangeType powerStateType)
    {
        await TryInvoke(async () =>
        {
            var powerDto = new PowerStateChangeDto(powerStateType);
            var signedDto = _keyProvider.CreateSignedDto(powerDto, DtoType.PowerStateChange, _appState.PrivateKey);
            await Connection.InvokeAsync(nameof(IViewerHub.SendSignedDtoToAgent), device.Id, signedDto);
        });
    }

    public async Task SendReadySignalToStreamer(string streamerConnectionId)
    {
        await TryInvoke(async () =>
        {
            var powerDto = new ViewerReadyForStreamDto();
            var signedDto = _keyProvider.CreateSignedDto(powerDto, DtoType.ViewerReadyForStream, _appState.PrivateKey);
            await Connection.InvokeAsync(nameof(IViewerHub.SendSignedDtoToStreamer), streamerConnectionId, signedDto);
        });
    }

    public async Task<Result> SendTerminalInput(string agentConnectionId, Guid terminalId, string input)
    {
        return await TryInvoke(
            async () =>
            {
                var request = new TerminalInputDto(terminalId, input);
                var signedDto = _keyProvider.CreateSignedDto(request, DtoType.TerminalInput, _appState.PrivateKey);
                return await Connection.InvokeAsync<Result>(nameof(IViewerHub.SendTerminalInput), agentConnectionId, signedDto);
            },
            () => Result.Fail("Failed to send terminal input"));
    }

    public async Task SendTypeText(string streamerConnectionId, string text)
    {
        await TryInvoke(
             async () =>
             {
                 await WaitForConnection();
                 var dto = new TypeTextDto(text);
                 var signedDto = _keyProvider.CreateSignedDto(dto, DtoType.TypeText, _appState.PrivateKey);
                 await Connection.InvokeAsync(nameof(IViewerHub.SendSignedDtoToStreamer), streamerConnectionId, signedDto);
             });
    }

    public async Task SendWakeDevice(string[] macAddresses)
    {
        await TryInvoke(
            async () =>
            {
                var request = new WakeDeviceDto(macAddresses);
                var signedDto = _keyProvider.CreateSignedDto(request, DtoType.WakeDevice, _appState.PrivateKey);
                await Connection.InvokeAsync(nameof(IViewerHub.SendSignedDtoToPublicKeyGroup), signedDto);
            });
    }

    public async Task SendWheelScroll(string streamerConnectionId, double percentX, double percentY, double scrollY, double scrollX)
    {
        await TryInvoke(
            async () =>
            {
                var dto = new WheelScrollDto(percentX, percentY, scrollY, scrollX);
                var signedDto = _keyProvider.CreateSignedDto(dto, DtoType.WheelScroll, _appState.PrivateKey);
                await Connection.InvokeAsync(nameof(IViewerHub.SendSignedDtoToStreamer), streamerConnectionId, signedDto);
            });
    }

    public async Task Start(CancellationToken cancellationToken)
    {
        _messenger.UnregisterAll(this);

        await _delayer.WaitForAsync(() => _appState.IsAuthenticated, TimeSpan.MaxValue);

        using var _ = _appState.IncrementBusyCounter();

        await Connect(
            () => $"{_settings.ServerUri}/hubs/viewer",
            ConfigureConnection,
            ConfigureHttpOptions,
            OnConnectFailure,
            useReconnect: true,
            cancellationToken);

        _messenger.RegisterGenericMessage(this, HandleGenericMessage);

        await PerformAfterConnectInit();
    }

    private async Task CheckIfServerAdministrator()
    {
        await TryInvoke(
            async () =>
            {
                _appState.IsServerAdministrator = await Connection.InvokeAsync<bool>(nameof(IViewerHub.CheckIfServerAdministrator));
                await _messenger.SendGenericMessage(GenericMessageKind.IsServerAdminChanged);
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
        connection.On<StreamerDownloadProgressDto>(nameof(ReceiveStreamerDownloadProgress), ReceiveStreamerDownloadProgress);
        connection.On<StreamerInitDataDto>(nameof(ReceiveStreamerInitData), ReceiveStreamerInitData);
        connection.On<ClipboardChangeDto>(nameof(ReceiveClipboardChanged), ReceiveClipboardChanged);
        connection.On<Guid>(nameof(ReceiveStreamerDisconnected), ReceiveStreamerDisconnected);
    }

    private void ConfigureHttpOptions(HttpConnectionOptions options)
    {
        var signature = _httpConfigurer.GetDigitalSignature();
        options.Headers["Authorization"] = $"{AuthSchemes.DigitalSignature} {signature}";
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

    private async Task HandleAuthStateChanged()
    {
        await StopConnection(_appState.AppExiting);

        if (_appState.IsAuthenticated)
        {
            await Start(_appState.AppExiting);
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

            default:
                break;
        }
    }

    private async Task HandleServerUriChanged()
    {
        await Reconnect(_appState.AppExiting);
    }

    private async Task OnConnectFailure(string reason)
    {
        await _messenger.Send(new ToastMessage(reason, Severity.Error));
    }

    private async Task PerformAfterConnectInit()
    {
        await CheckIfServerAdministrator();
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