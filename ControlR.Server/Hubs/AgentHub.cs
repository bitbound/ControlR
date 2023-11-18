using ControlR.Shared.Dtos;
using ControlR.Shared.Interfaces.HubClients;
using ControlR.Shared.Services;
using Microsoft.AspNetCore.SignalR;

namespace ControlR.Server.Hubs;

public class AgentHub(
    IHubContext<ViewerHub, IViewerHubClient> _viewerHub,
    ISystemTime _systemTime,
    ILogger<AgentHub> _logger) : Hub<IAgentHubClient>
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while updating device.");
        }
    }
}