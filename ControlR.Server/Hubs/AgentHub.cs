using ControlR.Server.Models;
using ControlR.Server.Services;
using ControlR.Shared.Dtos;
using ControlR.Shared.Interfaces.HubClients;
using ControlR.Shared.Models;
using ControlR.Shared.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using System.Buffers;
using System.Net.WebSockets;

namespace ControlR.Server.Hubs;

public class AgentHub(
    IHubContext<ViewerHub, IViewerHubClient> viewerHubContext,
    IProxyStreamStore proxyStreamStore,
    IOptionsMonitor<AppOptions> appOptions,
    ISystemTime systemTime,
    IHostApplicationLifetime appLifetime,
    ILogger<AgentHub> logger) : Hub<IAgentHubClient>
{
    private readonly IHostApplicationLifetime _appLifetime = appLifetime;
    private readonly IOptionsMonitor<AppOptions> _appOptions = appOptions;
    private readonly ILogger<AgentHub> _logger = logger;
    private readonly IProxyStreamStore _proxyStreamStore = proxyStreamStore;
    private readonly ISystemTime _systemTime = systemTime;
    private readonly IHubContext<ViewerHub, IViewerHubClient> _viewerHub = viewerHubContext;

    private DeviceDto? Device
    {
        get
        {
            if (Context.Items.TryGetValue(nameof(Device), out var cachedItem) &&
                cachedItem is DeviceDto deviceDto)
            {
                return deviceDto;
            }
            return null;
        }
        set
        {
            Context.Items[nameof(Device)] = value;
        }
    }

    public IceServer[] GetIceServers()
    {
        return [.. _appOptions.CurrentValue.IceServers];
    }

    public async IAsyncEnumerable<byte[]> GetVncStream(Guid sessionId)
    {
        if (!_proxyStreamStore.TryGet(sessionId, out var signaler))
        {
            yield break;
        }

        try
        {
            if (!await signaler.ReadySignal.WaitAsync(TimeSpan.FromSeconds(30)))
            {
                yield break;
            }

            if (signaler.NoVncWebsocket is not WebSocket ws)
            {
                _logger.LogWarning("WebSocket was null when it should have been populated.");
                yield break;
            }

            while (ws.State == WebSocketState.Open &&
                    !_appLifetime.ApplicationStopping.IsCancellationRequested)
            {
                var buffer = ArrayPool<byte>.Shared.Rent(ushort.MaxValue);

                var readResult = await ws.ReceiveAsync(buffer, _appLifetime.ApplicationStopping);
                yield return buffer[0..readResult.Count];

                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
        finally
        {
            signaler.EndSignal.Release();
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Device is DeviceDto cachedDevice)
        {
            cachedDevice.IsOnline = false;
            cachedDevice.LastSeen = _systemTime.Now;

            await _viewerHub.Clients.Groups(cachedDevice.AuthorizedKeys).ReceiveDeviceUpdate(cachedDevice);

            foreach (var key in Device.AuthorizedKeys)
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, key);
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task SendVncDownloadProgress(Guid streamingSessionId, string viewerConnectionId, double downloadProgress)
    {
        await _viewerHub.Clients.Client(viewerConnectionId).ReceiveVncDownloadProgress(streamingSessionId, downloadProgress);
    }

    public async Task SendVncStream(Guid sessionId, IAsyncEnumerable<byte[]> outgoingStream)
    {
        if (!_proxyStreamStore.TryGet(sessionId, out var signaler))
        {
            return;
        }

        try
        {
            if (!await signaler.ReadySignal.WaitAsync(TimeSpan.FromSeconds(30)))
            {
                return;
            }

            if (signaler.NoVncWebsocket is not WebSocket ws)
            {
                _logger.LogWarning("WebSocket was null when it should have been populated.");
                return;
            }

            await foreach (var chunk in outgoingStream)
            {
                if (ws.State != WebSocketState.Open ||
                    _appLifetime.ApplicationStopping.IsCancellationRequested)
                {
                    break;
                }

                await ws.SendAsync(chunk, WebSocketMessageType.Binary, true, _appLifetime.ApplicationStopping);
            }
        }
        finally
        {
            signaler.EndSignal.Release();
        }
    }

    public async Task UpdateDevice(DeviceDto device)
    {
        device.ConnectionId = Context.ConnectionId;
        device.IsOnline = true;
        device.LastSeen = _systemTime.Now;

        await Groups.AddToGroupAsync(Context.ConnectionId, device.Id);

        if (Device is DeviceDto cachedDevice)
        {
            var oldKeys = cachedDevice.AuthorizedKeys.Except(device.AuthorizedKeys);
            foreach (var oldKey in oldKeys)
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, oldKey);
            }

            var newKeys = device.AuthorizedKeys.Except(cachedDevice.AuthorizedKeys);
            foreach (var newKey in newKeys)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, newKey);
            }
        }
        else
        {
            foreach (var key in device.AuthorizedKeys)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, key);
            }
        }

        Device = device;
        await _viewerHub.Clients.Groups(device.AuthorizedKeys).ReceiveDeviceUpdate(device);
    }
}