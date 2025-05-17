using System.Net.Sockets;
using ControlR.Libraries.Shared.Dtos.HubDtos;
using Microsoft.AspNetCore.SignalR;
using DeviceDto = ControlR.Libraries.Shared.Dtos.ServerApi.DeviceDto;

namespace ControlR.Web.Server.Hubs;

public class AgentHub(
  AppDb appDb,
  TimeProvider timeProvider,
  IHubContext<ViewerHub, IViewerHubClient> viewerHub,
  IWebHostEnvironment hostEnvironment,
  IDeviceManager deviceManager,
  IOptions<AppOptions> appOptions,
  ILogger<AgentHub> logger) : HubWithItems<IAgentHubClient>, IAgentHub
{
  private readonly AppDb _appDb = appDb;
  private readonly IOptions<AppOptions> _appOptions = appOptions;
  private readonly IDeviceManager _deviceManager = deviceManager;
  private readonly IWebHostEnvironment _hostEnvironment = hostEnvironment;
  private readonly ILogger<AgentHub> _logger = logger;
  private readonly TimeProvider _timeProvider = timeProvider;
  private readonly IHubContext<ViewerHub, IViewerHubClient> _viewerHub = viewerHub;

  private DeviceDto? Device
  {
    get => GetItem<DeviceDto?>(null);
    set => SetItem(value);
  }

  public override async Task OnDisconnectedAsync(Exception? exception)
  {
    try
    {
      if (Device is { } cachedDeviceDto)
      {
        var dto = cachedDeviceDto with
        {
          IsOnline = false,
          LastSeen = _timeProvider.GetLocalNow()
        };

        var updateResult = await UpdateDeviceEntity(dto);
        if (updateResult.IsSuccess)
        {
          await SendDeviceUpdate(updateResult.Value, dto);
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

  public async Task<Result<DeviceDto>> UpdateDevice(DeviceDto deviceDto)
  {
    try
    {
      if (_hostEnvironment.IsDevelopment() && deviceDto.TenantId == Guid.Empty)
      {
        var lastTenant = await _appDb.Tenants
          .OrderByDescending(x => x.CreatedAt)
          .FirstOrDefaultAsync();

        if (lastTenant is null)
        {
          return Result.Fail<DeviceDto>("No tenants found.");
        }

        deviceDto = deviceDto with { TenantId = lastTenant.Id };
      }

      if (deviceDto.TenantId == Guid.Empty)
      {
        return Result.Fail<DeviceDto>("Invalid tenant ID.");
      }

      if (!await _appDb.Tenants.AnyAsync(x => x.Id == deviceDto.TenantId))
      {
        await Clients.Caller.UninstallAgent("Invalid tenant ID.");
        return Result.Fail<DeviceDto>("Invalid tenant ID.");
      }

      deviceDto = UpdateDtoState(deviceDto);

      var updateResult = await UpdateDeviceEntity(deviceDto);

      if (!updateResult.IsSuccess)
      {
        return Result.Fail<DeviceDto>(updateResult.Reason);
      }

      var deviceEntity = updateResult.Value;
      await AddToGroups(deviceEntity);

      Device = deviceEntity.ToDto();

      await SendDeviceUpdate(deviceEntity, Device);

      return Result.Ok(Device);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while updating device.");
      return Result.Fail<DeviceDto>("An error occurred while updating the device.");
    }
  }

  private async Task AddToGroups(Device deviceEntity)
  {
    if (Device is not null)
    {
      return;
    }

    await Groups.AddToGroupAsync(Context.ConnectionId, HubGroupNames.GetTenantDevicesGroupName(deviceEntity.TenantId));
    await Groups.AddToGroupAsync(Context.ConnectionId, HubGroupNames.GetDeviceGroupName(deviceEntity.Id, deviceEntity.TenantId));
    if (deviceEntity.Tags is { Count: > 0 } tags)
    {
      foreach (var tag in tags)
      {
        await Groups.AddToGroupAsync(Context.ConnectionId, HubGroupNames.GetTagGroupName(tag.Id, deviceEntity.TenantId));
      }
    }
  }

  private async Task SendDeviceUpdate(Device device, DeviceDto dto)
  {
    await _viewerHub.Clients
      .Group(HubGroupNames.GetUserRoleGroupName(RoleNames.DeviceSuperUser, device.TenantId))
      .ReceiveDeviceUpdate(dto);

    if (device.Tags is null)
    {
      return;
    }

    var groupNames = device.Tags.Select(x => HubGroupNames.GetTagGroupName(x.Id, x.TenantId));
    await _viewerHub.Clients.Groups(groupNames).ReceiveDeviceUpdate(dto);
  }

  private async Task<Result<Device>> UpdateDeviceEntity(DeviceDto dto)
  {
    // In dev, we can create the device to bootstrap it.
    if (_hostEnvironment.IsDevelopment())
    {
      var device = await _deviceManager.AddOrUpdate(dto, addTagIds: false);
      return Result.Ok(device);
    }

    var updateResult = await _deviceManager.UpdateDevice(dto, addTagIds: false);
    if (!updateResult.IsSuccess)
    {
      await Clients.Caller.UninstallAgent(updateResult.Reason);
    }
    return updateResult;
  }

  private DeviceDto UpdateDtoState(DeviceDto deviceDto)
  {
    deviceDto = deviceDto with
    {
      IsOnline = true,
      LastSeen = _timeProvider.GetLocalNow(),
      ConnectionId = Context.ConnectionId
    };

    var remoteIp = Context.GetHttpContext()?.Connection.RemoteIpAddress;
    if (remoteIp is not null)
    {
      if (remoteIp.AddressFamily == AddressFamily.InterNetworkV6)
      {
        deviceDto = deviceDto with { PublicIpV6 = remoteIp.ToString() };
      }
      else
      {
        deviceDto = deviceDto with { PublicIpV4 = remoteIp.ToString() };
      }
    }
    return deviceDto;
  }
}