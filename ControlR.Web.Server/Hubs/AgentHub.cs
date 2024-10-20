using Microsoft.AspNetCore.SignalR;
using System.Net.Sockets;

namespace ControlR.Web.Server.Hubs;

public class AgentHub(
  AppDb _appDb,
  IHubContext<ViewerHub, IViewerHubClient> _viewerHub,
  ISystemTime _systemTime,
  IServerStatsProvider _serverStatsProvider,
  IConnectionCounter _connectionCounter,
  IWebHostEnvironment _hostEnvironment,
  ILogger<AgentHub> _logger) : HubWithItems<IAgentHubClient>, IAgentHub
{
  private DeviceDto? Device
  {
    get => GetItem<DeviceDto?>(null);
    set => SetItem(value);
  }

  private Guid? TenantUid
  {
    get => GetItem<Guid?>(null);
    set => SetItem(value);
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

      if (Device is { } cachedDevice)
      {
        cachedDevice.IsOnline = false;
        cachedDevice.LastSeen = _systemTime.Now;
        await _viewerHub.Clients
          .Group(HubGroupNames.ServerAdministrators)
          .ReceiveDeviceUpdate(cachedDevice);

        await _appDb.AddOrUpdate<DeviceDto, Device>(cachedDevice);

        await SendDeviceUpdate();
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

  public async Task<Result<DeviceDto>> UpdateDevice(DeviceFromAgentDto device)
  {
    try
    {
      device.IsOnline = true;
      device.LastSeen = _systemTime.Now;
      device.ConnectionId = Context.ConnectionId;

      var remoteIp = Context.GetHttpContext()?.Connection.RemoteIpAddress;
      if (remoteIp is not null)
      {
        if (remoteIp.AddressFamily == AddressFamily.InterNetworkV6)
        {
          device.PublicIpV6 = remoteIp.ToString();
        }
        else
        {
          device.PublicIpV4 = remoteIp.ToString();
        }
      }

      var deviceEntity = await _appDb
        .AddOrUpdate<DeviceFromAgentDto, Device>(
          device, 
          [x => x.Tenant]);

      if (_hostEnvironment.IsDevelopment() && deviceEntity.TenantId is null)
      {
        var firstTenant = await _appDb.Tenants.OrderBy(x => x.Id).FirstOrDefaultAsync();
        deviceEntity.TenantId = firstTenant?.Id;
        await _appDb.SaveChangesAsync();
      }

      TenantUid = deviceEntity.Tenant?.Id;
      Device = deviceEntity.ToDto();

      Device.ConnectionId = Context.ConnectionId;

      await Groups.AddToGroupAsync(Context.ConnectionId, HubGroupNames.GetDeviceGroupName(Device.Id));

      await SendDeviceUpdate();

      return Result.Ok(Device);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while updating device.");
      return Result.Fail<DeviceDto>("An error occurred while updating the device.");
    }
  }

  private async Task SendDeviceUpdate()
  {
    if (Device is null)
    {
      return;
    }

    if (TenantUid.HasValue)
    {
      await _viewerHub.Clients
        .Group(HubGroupNames.GetDeviceAdministratorGroup(TenantUid.Value))
        .ReceiveDeviceUpdate(Device);
    }
  }
  private async Task SendUpdatedConnectionCountToAdmins()
  {
    try
    {
      var statsResult = await _serverStatsProvider.GetServerStats();
      if (statsResult.IsSuccess)
      {
        await _viewerHub.Clients
          .Group(HubGroupNames.ServerAdministrators)
          .ReceiveServerStats(statsResult.Value);
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while sending updated agent connection count to admins.");
    }
  }
}