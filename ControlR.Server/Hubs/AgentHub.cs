using ControlR.Libraries.Shared.Dtos.StreamerDtos;
using ControlR.Server.Services.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace ControlR.Server.Hubs;

public class AgentHub(
    IHubContext<ViewerHub, IViewerHubClient> _viewerHub,
    ISystemTime _systemTime,
    IConnectionCounter _connectionCounter,
    ILogger<AgentHub> _logger) : Hub<IAgentHubClient>, IAgentHub
{
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

    public override async Task OnConnectedAsync()
    {
        try
        {
            _connectionCounter.IncrementAgentCount();
            await SendUpdatedConnectionCountToAdmins();
            await base.OnConnectedAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during device connect.");
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        try
        {
            _connectionCounter.DecrementAgentCount();
            await SendUpdatedConnectionCountToAdmins();

            if (Device is DeviceDto cachedDevice)
            {
                cachedDevice.IsOnline = false;
                cachedDevice.LastSeen = _systemTime.Now;

                var publicKeys = cachedDevice.AuthorizedKeys
                    .Select(x => x.PublicKey)
                    .ToArray();

                await _viewerHub.Clients.Groups(publicKeys).ReceiveDeviceUpdate(cachedDevice);

                foreach (var key in publicKeys)
                {
                    await Groups.RemoveFromGroupAsync(Context.ConnectionId, key);
                }
            }

            await base.OnDisconnectedAsync(exception);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during device disconnect.");
        }
    }

    public async Task SendStreamerDownloadProgress(StreamerDownloadProgressDto progressDto)
    {
        await _viewerHub.Clients.Client(progressDto.ViewerConnectionId).ReceiveStreamerDownloadProgress(progressDto);
    }

    public async Task SendTerminalOutputToViewer(string viewerConnectionId, TerminalOutputDto outputDto)
    {
        try
        {
            await _viewerHub.Clients
                .Client(viewerConnectionId)
                .ReceiveTerminalOutput(outputDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while sending terminal output to viewer.");
        }
    }

    public async Task UpdateDevice(DeviceDto device)
    {
        try
        {
            device.ConnectionId = Context.ConnectionId;
            device.IsOnline = true;
            device.LastSeen = _systemTime.Now;

            await Groups.AddToGroupAsync(Context.ConnectionId, device.Id);

            var newPubKeys = device.AuthorizedKeys.Select(x => x.PublicKey).ToArray();

            if (Device is DeviceDto cachedDevice)
            {
                var oldKeys = cachedDevice.AuthorizedKeys
                    .ExceptBy(newPubKeys, x => x.PublicKey)
                    .Select(x => x.PublicKey)
                    .ToArray();

                foreach (var oldKey in oldKeys)
                {
                    await Groups.RemoveFromGroupAsync(Context.ConnectionId, oldKey);
                }

                var oldPubKeys = cachedDevice.AuthorizedKeys.Select(x => x.PublicKey).ToArray();
                var newKeys = device.AuthorizedKeys
                    .ExceptBy(oldPubKeys, x => x.PublicKey)
                    .Select(x => x.PublicKey)
                    .ToArray();

                foreach (var newKey in newPubKeys)
                {
                    await Groups.AddToGroupAsync(Context.ConnectionId, newKey);
                }
            }
            else
            {
                foreach (var key in newPubKeys)
                {
                    await Groups.AddToGroupAsync(Context.ConnectionId, key);
                }
            }

            Device = device;
            await _viewerHub.Clients.Groups(newPubKeys).ReceiveDeviceUpdate(device);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while updating device.");
        }
    }

    private async Task SendUpdatedConnectionCountToAdmins()
    {
        try
        {
            var agentResult = await _connectionCounter.GetAgentConnectionCount();
            var viewerResult = await _connectionCounter.GetViewerConnectionCount();

            if (!agentResult.IsSuccess)
            {
                _logger.LogResult(agentResult);
                return;
            }

            if (!viewerResult.IsSuccess)
            {
                _logger.LogResult(viewerResult);
                return;
            }

            var dto = new ServerStatsDto(
                agentResult.Value,
                viewerResult.Value);

            await _viewerHub.Clients
                .Group(HubGroupNames.ServerAdministrators)
                .ReceiveServerStats(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while sending updated agent connection count to admins.");
        }
    }
}