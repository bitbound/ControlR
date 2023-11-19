using Bitbound.SimpleMessenger;
using ControlR.Devices.Common.Extensions;
using ControlR.Devices.Common.Services;
using ControlR.Shared;
using ControlR.Shared.Dtos;
using ControlR.Shared.Enums;
using ControlR.Shared.Helpers;
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
    Task CloseTerminalSession(string agentConnectionId, Guid terminalId);

    Task<Result<TerminalSessionRequestResult>> CreateTerminalSession(string agentConnectionId, Guid terminalId);

    Task<VncSessionRequestResult> GetVncSession(string agentConnectionId, Guid sessionId, string vncPassword);

    Task<Result<WindowsSession[]>> GetWindowsSessions(DeviceDto device);

    Task Reconnect(CancellationToken cancellationToken);

    Task RequestDeviceUpdates();

    Task SendPowerStateChange(DeviceDto device, PowerStateChangeType powerStateType);

    Task<Result> SendTerminalInput(string agentConnectionId, Guid terminalId, string input);

    Task Start(CancellationToken cancellationToken);

    Task<Result> StartRdpProxy(string agentConnectionId, Guid sessionId);
}

internal class ViewerHubConnection(
    IServiceScopeFactory serviceScopeFactory,
    IHttpConfigurer _httpConfigurer,
    IAppState _appState,
    IDeviceCache _devicesCache,
    IKeyProvider _keyProvider,
    ISettings _settings,
    ILogger<ViewerHubConnection> _logger,
    IMessenger messenger) : HubConnectionBase(serviceScopeFactory, messenger, _logger), IViewerHubConnection, IViewerHubClient
{
    public async Task CloseTerminalSession(string deviceId, Guid terminalId)
    {
        await TryInvoke(async () =>
        {
            var request = new CloseTerminalRequest(terminalId);
            var signedDto = _keyProvider.CreateSignedDto(request, DtoType.CloseTerminalRequest, _appState.UserKeys.PrivateKey);
            await Connection.InvokeAsync("SendSignedDtoToAgent", deviceId, signedDto);
        });
    }

    public async Task<Result<TerminalSessionRequestResult>> CreateTerminalSession(string agentConnectionId, Guid terminalId)
    {
        return await TryInvoke(
            async () =>
            {
                Guard.IsNotNull(Connection.ConnectionId);

                var request = new TerminalSessionRequest(terminalId, Connection.ConnectionId);
                var signedDto = _keyProvider.CreateSignedDto(request, DtoType.TerminalSessionRequest, _appState.UserKeys.PrivateKey);
                return await Connection.InvokeAsync<Result<TerminalSessionRequestResult>>("CreateTerminalSession", agentConnectionId, signedDto);
            },
            () => Result.Fail<TerminalSessionRequestResult>("Failed to create terminal session."));
    }

    public async Task<VncSessionRequestResult> GetVncSession(string agentConnectionId, Guid sessionId, string vncPassword)
    {
        return await TryInvoke(
            async () =>
            {
                var vncSession = new VncSessionRequest(sessionId, vncPassword);
                var signedDto = _keyProvider.CreateSignedDto(vncSession, DtoType.VncSessionRequest, _appState.UserKeys.PrivateKey);

                var result = await Connection.InvokeAsync<VncSessionRequestResult>("GetVncSession", agentConnectionId, sessionId, signedDto);
                if (!result.SessionCreated)
                {
                    _logger.LogError("Failed to get VNC session.");
                }
                return result;
            },
            () => new(false));
    }

    public async Task<Result<WindowsSession[]>> GetWindowsSessions(DeviceDto device)
    {
        try
        {
            var signedDto = _keyProvider.CreateRandomSignedDto(DtoType.WindowsSessions, _appState.UserKeys.PrivateKey);
            var sessions = await Connection.InvokeAsync<WindowsSession[]>("GetWindowsSessions", device.ConnectionId, signedDto);
            return Result.Ok(sessions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while getting windows sessions.");
            return Result.Fail<WindowsSession[]>(ex);
        }
    }

    public Task ReceiveDeviceUpdate(DeviceDto device)
    {
        _devicesCache.AddOrUpdate(device);
        _messenger.SendGenericMessage(GenericMessageKind.DevicesCacheUpdated);
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
            }
            catch { }
            await Start(cancellationToken);
            break;
        }
    }

    public async Task RequestDeviceUpdates()
    {
        await TryInvoke(async () =>
        {
            await WaitForConnection();
            var signedDto = _keyProvider.CreateRandomSignedDto(DtoType.DeviceUpdateRequest, _appState.UserKeys.PrivateKey);
            await Connection.InvokeAsync("SendSignedDtoToPublicKeyGroup", signedDto);
        });
    }

    public async Task SendPowerStateChange(DeviceDto device, PowerStateChangeType powerStateType)
    {
        await TryInvoke(async () =>
        {
            var powerDto = new PowerStateChangeDto(powerStateType);
            var signedDto = _keyProvider.CreateSignedDto(powerDto, DtoType.PowerStateChange, _appState.UserKeys.PrivateKey);
            await Connection.InvokeAsync("SendSignedDtoToAgent", device.Id, signedDto);
        });
    }

    public async Task<Result> SendTerminalInput(string agentConnectionId, Guid terminalId, string input)
    {
        return await TryInvoke(
            async () =>
            {
                var request = new TerminalInputDto(terminalId, input);
                var signedDto = _keyProvider.CreateSignedDto(request, DtoType.TerminalInput, _appState.UserKeys.PrivateKey);
                return await Connection.InvokeAsync<Result>("SendTerminalInput", agentConnectionId, signedDto);
            },
            () => Result.Fail("Failed to send terminal input"));
    }

    public async Task Start(CancellationToken cancellationToken)
    {
        _messenger.UnregisterAll(this);

        await WaitHelper.WaitForAsync(() => _appState.IsAuthenticated, TimeSpan.MaxValue);

        using var _ = _appState.IncrementBusyCounter();

        await Connect(
            $"{_settings.ServerUri}/hubs/viewer",
            ConfigureConnection,
            ConfigureHttpOptions,
            OnConnectFailure,
            cancellationToken);

        _messenger.RegisterGenericMessage(this, GenericMessageKind.AuthStateChanged, HandleAuthStateChanged);

        await RequestDeviceUpdates();
    }

    public async Task<Result> StartRdpProxy(string agentConnectionId, Guid sessionId)
    {
        return await TryInvoke(
        async () =>
        {
            var dto = new RdpProxyRequestDto(sessionId);
            var signedDto = _keyProvider.CreateSignedDto<RdpProxyRequestDto>(dto, DtoType.StartRdpProxy, _appState.UserKeys.PrivateKey);

            var result = await Connection.InvokeAsync<Result>("StartRdpProxy", agentConnectionId, sessionId, signedDto);
            if (!result.IsSuccess)
            {
                _logger.LogError("Failed to start RDP proxy session.");
            }
            return result;
        },
        () => new(false));
    }

    private void ConfigureConnection(HubConnection connection)
    {
        connection.Closed += Connection_Closed;
        connection.Reconnecting += Connection_Reconnecting;
        connection.Reconnected += Connection_Reconnected;
        connection.On<DeviceDto>(nameof(ReceiveDeviceUpdate), ReceiveDeviceUpdate);
        connection.On<TerminalOutputDto>(nameof(ReceiveTerminalOutput), ReceiveTerminalOutput);
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