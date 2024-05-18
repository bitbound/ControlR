using Bitbound.SimpleMessenger;
using ControlR.Devices.Common.Extensions;
using ControlR.Devices.Common.Services;
using ControlR.Shared;
using ControlR.Shared.Dtos;
using ControlR.Shared.Enums;
using ControlR.Shared.Helpers;
using ControlR.Shared.Hubs;
using ControlR.Shared.Interfaces.HubClients;
using ControlR.Shared.Models;
using ControlR.Shared.Services;
using ControlR.Viewer.Models.Messages;
using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using MudBlazor;
using System.Runtime.CompilerServices;

namespace ControlR.Viewer.Services;

public interface IViewerHubConnection : IHubConnectionBase
{
    Task ClearAlert();

    Task CloseStreamingSession(Guid sessionId);

    Task CloseTerminalSession(string agentConnectionId, Guid terminalId);

    Task<Result<TerminalSessionRequestResult>> CreateTerminalSession(string agentConnectionId, Guid terminalId);

    Task<Result<AgentAppSettings>> GetAgentAppSettings(string agentConnectionId);

    Task<Result<AlertBroadcastDto>> GetCurrentAlertFromServer();

    Task<Result<DisplayDto[]>> GetDisplays(string desktopConnectionId);

    Task<Result<IceServer[]>> GetIceServers();

    Task<Result<ServerStatsDto>> GetServerStats();

    Task<Result<StreamerHubSession>> GetStreamingSession(string agentConnectionId, Guid sessionId, int targetSystemSession);

    Task<Result<WindowsSession[]>> GetWindowsSessions(DeviceDto device);

    Task InvokeCtrlAltDel(string deviceId);

    Task Reconnect(CancellationToken cancellationToken);

    Task RequestDeviceUpdates();

    Task<Result> SendAgentAppSettings(string agentConnectionId, AgentAppSettings agentAppSettings);

    Task SendAlertBroadcast(string message, AlertSeverity severity, bool isSticky);
    Task SendIceCandidate(Guid sessionId, string iceCandidateJson);

    Task SendPowerStateChange(DeviceDto device, PowerStateChangeType powerStateType);

    Task SendRtcSessionDescription(Guid sessionId, RtcSessionDescription sessionDescription);

    Task<Result> SendTerminalInput(string agentConnectionId, Guid terminalId, string input);

    Task SendWakeDevice(string[] macAddresses);

    Task Start(CancellationToken cancellationToken);

}

internal class ViewerHubConnection(
    IServiceScopeFactory _serviceScopeFactory,
    IHttpConfigurer _httpConfigurer,
    IAppState _appState,
    IDeviceCache _devicesCache,
    IKeyProvider _keyProvider,
    ISettings _settings,
    IDelayer _delayer,
    IMessenger _messenger,
    ILogger<ViewerHubConnection> _logger) : HubConnectionBase(_serviceScopeFactory, _messenger, _delayer, _logger), IViewerHubConnection, IViewerHubClient
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

    public async Task CloseStreamingSession(Guid sessionId)
    {
        await TryInvoke(
            async () =>
            {
                var signedDto = _keyProvider.CreateRandomSignedDto(DtoType.CloseStreamingSession, _appState.PrivateKey);
                await Connection.InvokeAsync(nameof(IViewerHub.SendSignedDtoToStreamer), sessionId, signedDto);
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
                    await _messenger.Send(alertResult.Value);
                }
                else if (alertResult.HadException)
                {
                    alertResult.Log(_logger);
                }
                return alertResult;
            },
            () => Result.Fail<AlertBroadcastDto>("Failed to get current alert from the server."));
    }

    public Task<Result<DisplayDto[]>> GetDisplays(string desktopConnectionId)
    {
        return Result.Fail<DisplayDto[]>("Not implemented.").AsTaskResult();
    }

    public async Task<Result<IceServer[]>> GetIceServers()
    {
        try
        {
            var iceServers = await Connection.InvokeAsync<IceServer[]>(nameof(IViewerHub.GetIceServers));
            return Result.Ok(iceServers);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while getting ICE servers..");
            return Result.Fail<IceServer[]>(ex);
        }
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

    public async Task<Result<StreamerHubSession>> GetStreamingSession(string agentConnectionId, Guid sessionId, int targetSystemSession)
    {
        try
        {
            if (Connection.ConnectionId is null)
            {
                return Result.Fail<StreamerHubSession>("Connection has closed.");
            }

            var streamingSessionRequest = new StreamerSessionRequestDto(
                sessionId,
                targetSystemSession,
                Connection.ConnectionId,
                _settings.NotifyUserSessionStart,
                _settings.LowerUacDuringSession,
                _settings.Username);

            var signedDto = _keyProvider.CreateSignedDto(streamingSessionRequest, DtoType.StreamingSessionRequest, _appState.PrivateKey);

            var result = await Connection.InvokeAsync<Result<StreamerHubSession>>(nameof(IViewerHub.GetStreamingSession), agentConnectionId, sessionId, signedDto);
            if (!result.IsSuccess)
            {
                _logger.LogResult(result);
            }
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while getting remote streaming session.");
            return Result.Fail<StreamerHubSession>(ex);
        }
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
        await _messenger.Send(alert);
    }

    public Task ReceiveDesktopChanged(Guid sessionId)
    {
        _messenger.Send(new DesktopChangedMessage(sessionId));
        return Task.CompletedTask;
    }
    public Task ReceiveDeviceUpdate(DeviceDto device)
    {
        _devicesCache.AddOrUpdate(device);
        _messenger.SendGenericMessage(GenericMessageKind.DevicesCacheUpdated);
        return Task.CompletedTask;
    }

    public Task ReceiveIceCandidate(Guid sessionId, string candidateJson)
    {
        _messenger.Send(new IceCandidateMessage(sessionId, candidateJson));
        return Task.CompletedTask;
    }

    public Task ReceiveStreamerDownloadProgress(StreamerDownloadProgressDto progressDto)
    {
        _messenger.Send(new StreamerDownloadProgressMessage(progressDto.StreamingSessionId, progressDto.Progress, progressDto.Message));
        return Task.CompletedTask;
    }

    public Task ReceiveRtcSessionDescription(Guid sessionId, RtcSessionDescription sessionDescription)
    {
        _messenger.Send(new RtcSessionDescriptionMessage(sessionId, sessionDescription));
        return Task.CompletedTask;
    }
    public async Task ReceiveServerStats(ServerStatsDto serverStats)
    {
        var message = new ServerStatsUpdateMessage(serverStats);
        await _messenger.Send(message);
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
            var signedDto = _keyProvider.CreateRandomSignedDto(DtoType.DeviceUpdateRequest, _appState.PrivateKey);
            await Connection.InvokeAsync(nameof(IViewerHub.SendSignedDtoToPublicKeyGroup), signedDto);
        });
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

    public async Task SendAlertBroadcast(string message, AlertSeverity severity, bool isSticky)
    {
        await TryInvoke(
            async () =>
            {
                await WaitForConnection();
                var dto = new AlertBroadcastDto(message, severity, isSticky);
                var signedDto = _keyProvider.CreateSignedDto(dto, DtoType.SendAlertBroadcast, _appState.PrivateKey);
                await Connection.InvokeAsync<Result>(nameof(IViewerHub.SendAlertBroadcast), signedDto);
            });
    }

    public async Task SendIceCandidate(Guid sessionId, string iceCandidateJson)
    {
        await TryInvoke(async () =>
        {
            var signedDto = _keyProvider.CreateSignedDto(iceCandidateJson, DtoType.RtcIceCandidate, _appState.PrivateKey);
            await Connection.InvokeAsync("SendSignedDtoToStreamer", sessionId, signedDto);
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

    public async Task SendRtcSessionDescription(Guid sessionId, RtcSessionDescription sessionDescription)
    {
        await TryInvoke(async () =>
        {
            var signedDto = _keyProvider.CreateSignedDto(sessionDescription, DtoType.RtcSessionDescription, _appState.PrivateKey);
            await Connection.InvokeAsync(nameof(IViewerHub.SendSignedDtoToStreamer), sessionId, signedDto);
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
            cancellationToken);

        _messenger.RegisterGenericMessage(this, HandleGenericMessage);

        await CheckIfServerAdministrator();
        await CheckIfStoreIntegrationEnabled();
        await GetCurrentAlertFromServer();
        await RequestDeviceUpdates();
    }

    private async Task CheckIfStoreIntegrationEnabled()
    {
        await TryInvoke(
            async () =>
            {
                var isStoreEnabled = await Connection.InvokeAsync<bool>(nameof(IViewerHub.CheckIfStoreIntegrationEnabled));
                _appState.IsStoreIntegrationEnabled = isStoreEnabled;
            });
    }

    private async Task CheckIfServerAdministrator()
    {
        await TryInvoke(
            async () =>
            {
                var isAdmin = await Connection.InvokeAsync<bool>(nameof(IViewerHub.CheckIfServerAdministrator));
                if (_appState.IsServerAdministrator != isAdmin)
                {
                    _appState.IsServerAdministrator = isAdmin;
                    await _messenger.SendGenericMessage(GenericMessageKind.KeysStateChanged);
                }
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
        connection.On<Guid, string>(nameof(ReceiveIceCandidate), ReceiveIceCandidate);
        connection.On<Guid, RtcSessionDescription>(nameof(ReceiveRtcSessionDescription), ReceiveRtcSessionDescription);
        connection.On<StreamerDownloadProgressDto>(nameof(ReceiveStreamerDownloadProgress), ReceiveStreamerDownloadProgress);
        connection.On<Guid>(nameof(ReceiveDesktopChanged), ReceiveDesktopChanged);
    }

    private void ConfigureHttpOptions(HttpConnectionOptions options)
    {
        var signature = _httpConfigurer.GetDigitalSignature();
        options.Headers["Authorization"] = $"{AuthSchemes.DigitalSignature} {signature}";
    }

    private Task Connection_Closed(Exception? arg)
    {
        _messenger.SendGenericMessage(GenericMessageKind.HubConnectionStateChanged);
        return Task.CompletedTask;
    }

    private Task Connection_Reconnected(string? arg)
    {
        _messenger.SendGenericMessage(GenericMessageKind.HubConnectionStateChanged);
        return Task.CompletedTask;
    }

    private Task Connection_Reconnecting(Exception? arg)
    {
        _messenger.SendGenericMessage(GenericMessageKind.HubConnectionStateChanged);
        return Task.CompletedTask;
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

    private Task OnConnectFailure(string reason)
    {
        _messenger.Send(new ToastMessage(reason, Severity.Error));
        return Task.CompletedTask;
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