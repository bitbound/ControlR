using Microsoft.AspNetCore.SignalR;

namespace ControlR.Web.Server.Hubs;

public class AgentHub(
  IHubContext<ViewerHub, IViewerHubClient> viewerHub,
  ISystemTime systemTime,
  IServerStatsProvider serverStatsProvider,
  IConnectionCounter connectionCounter,
  ILogger<AgentHub> logger) : Hub<IAgentHubClient>, IAgentHub
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
    set => Context.Items[nameof(Device)] = value;
  }

  public async Task SendStreamerDownloadProgress(StreamerDownloadProgressDto progressDto)
  {
    await viewerHub.Clients.Client(progressDto.ViewerConnectionId).ReceiveStreamerDownloadProgress(progressDto);
  }

  public async Task SendTerminalOutputToViewer(string viewerConnectionId, TerminalOutputDto outputDto)
  {
    try
    {
      await viewerHub.Clients
        .Client(viewerConnectionId)
        .ReceiveTerminalOutput(outputDto);
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error while sending terminal output to viewer.");
    }
  }

  public async Task UpdateDevice(DeviceDto device)
  {
    try
    {
      device.ConnectionId = Context.ConnectionId;
      device.IsOnline = true;
      device.LastSeen = systemTime.Now;

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
      await viewerHub.Clients.Groups(newPubKeys).ReceiveDeviceUpdate(device);
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error while updating device.");
    }
  }

  public override async Task OnConnectedAsync()
  {
    try
    {
      connectionCounter.IncrementAgentCount();
      await SendUpdatedConnectionCountToAdmins();
      await base.OnConnectedAsync();
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error during device connect.");
    }
  }

  public override async Task OnDisconnectedAsync(Exception? exception)
  {
    try
    {
      connectionCounter.DecrementAgentCount();
      await SendUpdatedConnectionCountToAdmins();

      if (Device is DeviceDto cachedDevice)
      {
        cachedDevice.IsOnline = false;
        cachedDevice.LastSeen = systemTime.Now;

        var publicKeys = cachedDevice.AuthorizedKeys
          .Select(x => x.PublicKey)
          .ToArray();

        await viewerHub.Clients.Groups(publicKeys).ReceiveDeviceUpdate(cachedDevice);

        foreach (var key in publicKeys)
        {
          await Groups.RemoveFromGroupAsync(Context.ConnectionId, key);
        }
      }

      await base.OnDisconnectedAsync(exception);
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error during device disconnect.");
    }
  }

  private async Task SendUpdatedConnectionCountToAdmins()
  {
    try
    {
      var statsResult = await serverStatsProvider.GetServerStats();
      if (statsResult.IsSuccess)
      {
        await viewerHub.Clients
          .Group(HubGroupNames.ServerAdministrators)
          .ReceiveServerStats(statsResult.Value);
      }
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error while sending updated agent connection count to admins.");
    }
  }
}