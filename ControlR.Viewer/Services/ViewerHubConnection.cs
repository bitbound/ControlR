using CommunityToolkit.Mvvm.Messaging;
using ControlR.Devices.Common.Services;
using ControlR.Shared;
using ControlR.Shared.Dtos;
using ControlR.Shared.Enums;
using ControlR.Shared.Helpers;
using ControlR.Shared.Interfaces.HubClients;
using ControlR.Shared.Models;
using ControlR.Viewer.Extensions;
using ControlR.Viewer.Models.Messages;
using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using MudBlazor;
using System.Runtime.CompilerServices;

namespace ControlR.Viewer.Services;

public interface IViewerHubConnection : IHubConnectionBase
{
    Task<VncSessionRequestResult> GetVncSession(string agentConnectionId, Guid sessionId, string sessionPassword);

    Task<Result<WindowsSession[]>> GetWindowsSessions(DeviceDto device);

    Task RequestDeviceUpdates();

    Task SendPowerStateChange(DeviceDto device, PowerStateChangeType powerStateType);

    Task Start(CancellationToken cancellationToken);
}

internal class ViewerHubConnection(
    IServiceScopeFactory serviceScopeFactory,
    IHttpConfigurer httpConfigurer,
    IMessenger messenger,
    IAppState appState,
    ISettings settings,
    IDeviceCache devicesCache,
    ILogger<ViewerHubConnection> logger) : HubConnectionBase(serviceScopeFactory, logger), IViewerHubConnection, IViewerHubClient
{
    private readonly IAppState _appState = appState;
    private readonly IDeviceCache _devicesCache = devicesCache;
    private readonly IHttpConfigurer _httpConfigurer = httpConfigurer;
    private readonly IMessenger _messenger = messenger;
    private readonly ISettings _settings = settings;

    public async Task<Result<IceServer[]>> GetIceServers()
    {
        try
        {
            var signedDto = _appState.Encryptor.CreateRandomSignedDto(DtoType.None);
            var iceServers = await Connection.InvokeAsync<IceServer[]>("GetIceServers", signedDto);
            return Result.Ok(iceServers);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while getting ICE servers..");
            return Result.Fail<IceServer[]>(ex);
        }
    }

    public async Task<VncSessionRequestResult> GetVncSession(string agentConnectionId, Guid sessionId, string sessionPassword)
    {
        try
        {
            var vncSession = new VncSessionRequest(
                sessionId,
                sessionPassword,
                Connection.ConnectionId);

            var signedDto = _appState.Encryptor.CreateSignedDto(vncSession, DtoType.VncSessionRequest);

            var result = await Connection.InvokeAsync<VncSessionRequestResult>("GetVncSession", agentConnectionId, sessionId, signedDto);
            if (!result.SessionCreated)
            {
                _logger.LogError("Failed to get VNC session.");
            }
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while getting remote streaming session.");
            return new(false);
        }
    }

    public async Task<Result<WindowsSession[]>> GetWindowsSessions(DeviceDto device)
    {
        try
        {
            var signedDto = _appState.Encryptor.CreateRandomSignedDto(DtoType.WindowsSessions);
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
        _messenger.SendParameterlessMessage(ParameterlessMessageKind.DevicesCacheUpdated);
        return Task.CompletedTask;
    }

    public async Task RequestDeviceUpdates()
    {
        await TryInvoke(async () =>
        {
            await WaitForConnection();
            var signedDto = _appState.Encryptor.CreateRandomSignedDto(DtoType.DeviceUpdateRequest);
            await Connection.InvokeAsync("SendSignedDtoToPublicKeyGroup", signedDto);
        });
    }

    public async Task SendPowerStateChange(DeviceDto device, PowerStateChangeType powerStateType)
    {
        await TryInvoke(async () =>
        {
            var powerDto = new PowerStateChangeDto(powerStateType);
            var signedDto = _appState.Encryptor.CreateSignedDto(powerDto, DtoType.PowerStateChange);
            await Connection.InvokeAsync("SendSignedDtoToAgent", device.Id, signedDto);
        });
    }

    public async Task Start(CancellationToken cancellationToken)
    {
        _messenger.UnregisterAll(this);

        await WaitHelper.WaitForAsync(() => _appState.IsAuthenticated, TimeSpan.MaxValue);

        using var _ = _appState.IncrementBusyCounter();

        await Connect(
            $"{AppConstants.ServerUri}/hubs/viewer",
            ConfigureConnection,
            ConfigureHttpOptions,
            OnConnectFailure,
            cancellationToken);

        _messenger.RegisterParameterless(this, ParameterlessMessageKind.AuthStateChanged, HandleAuthStateChanged);

        await RequestDeviceUpdates();
    }

    private void ConfigureConnection(HubConnection connection)
    {
        connection.Closed += Connection_Closed;
        connection.Reconnecting += Connection_Reconnecting;
        connection.Reconnected += Connection_Reconnected;
        connection.On<DeviceDto>(nameof(ReceiveDeviceUpdate), ReceiveDeviceUpdate);
    }

    private void ConfigureHttpOptions(HttpConnectionOptions options)
    {
        var signature = _httpConfigurer.GetDigitalSignature();
        options.Headers["Authorization"] = $"{AuthSchemes.DigitalSignature} {signature}";
    }

    private Task Connection_Closed(Exception? arg)
    {
        _messenger.SendParameterlessMessage(ParameterlessMessageKind.HubConnectionStateChanged);
        return Task.CompletedTask;
    }

    private Task Connection_Reconnected(string? arg)
    {
        _messenger.SendParameterlessMessage(ParameterlessMessageKind.HubConnectionStateChanged);
        return Task.CompletedTask;
    }

    private Task Connection_Reconnecting(Exception? arg)
    {
        _messenger.SendParameterlessMessage(ParameterlessMessageKind.HubConnectionStateChanged);
        return Task.CompletedTask;
    }

    private async Task HandleAuthStateChanged()
    {
        await StopConnection(_appState.AppExiting);

        if (_appState.AuthenticationState == Enums.AuthenticationState.PrivateKeyLoaded)
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

    private class RetryPolicy : IRetryPolicy
    {
        public TimeSpan? NextRetryDelay(RetryContext retryContext)
        {
            return TimeSpan.FromSeconds(3);
        }
    }
}