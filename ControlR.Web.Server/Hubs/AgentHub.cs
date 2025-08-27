using System.Net.Sockets;
using ControlR.Libraries.Shared.Dtos.HubDtos;
using ControlR.Libraries.Shared.Helpers;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.SignalR;
using DeviceDto = ControlR.Libraries.Shared.Dtos.ServerApi.DeviceDto;

namespace ControlR.Web.Server.Hubs;

public class AgentHub(
  AppDb appDb,
  TimeProvider timeProvider,
  IHubContext<ViewerHub, IViewerHubClient> viewerHub,
  IDeviceManager deviceManager,
  IOptions<AppOptions> appOptions,
  IOutputCacheStore outputCacheStore,
  IHubStreamStore hubStreamStore,
  ILogger<AgentHub> logger) : HubWithItems<IAgentHubClient>, IAgentHub
{
  private readonly AppDb _appDb = appDb;
  private readonly IOptions<AppOptions> _appOptions = appOptions;
  private readonly IDeviceManager _deviceManager = deviceManager;
  private readonly IHubStreamStore _hubStreamStore = hubStreamStore;
  private readonly ILogger<AgentHub> _logger = logger;
  private readonly IOutputCacheStore _outputCacheStore = outputCacheStore;
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
        // Check if this is still the current connection for this device
        var deviceConnectionId = await _appDb.Devices
          .Where(d => d.Id == cachedDeviceDto.Id)
          .Select(d => d.ConnectionId)
          .FirstOrDefaultAsync();

        // Only mark offline if this was the current connection
        if (deviceConnectionId == Context.ConnectionId)
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
        else
        {
          _logger.LogDebug(
            "Skipping offline update. Device has reconnected with connection {CurrentConnectionId}. Disconnecting {OldConnectionId}.",
            deviceConnectionId, Context.ConnectionId);
        }
      }
      await base.OnDisconnectedAsync(exception);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error during device disconnect.");
    }
  }

  public async Task SendDesktopClientDownloadProgress(DesktopClientDownloadProgressDto progressDto)
  {
    await _viewerHub.Clients
      .Client(progressDto.ViewerConnectionId)
      .ReceiveDesktopClientDownloadProgress(progressDto);
  }

  public async Task SendChatResponse(ChatResponseHubDto responseDto)
  {
    try
    {
      await _viewerHub.Clients
        .Client(responseDto.ViewerConnectionId)
        .ReceiveChatResponse(responseDto);
      
      _logger.LogInformation(
        "Chat response forwarded to viewer {ViewerConnectionId} for session {SessionId}",
        responseDto.ViewerConnectionId,
        responseDto.SessionId);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error forwarding chat response to viewer.");
    }
  }

  public async Task SendDesktopPreviewStream(Guid streamId, IAsyncEnumerable<byte[]> jpegChunks)
  {
    try
    {
      _logger.LogInformation("Receiving desktop preview stream for stream ID: {StreamId}", streamId);
      
      if (!_hubStreamStore.TryGet(streamId, out var signaler))
      {
        _logger.LogWarning("No signaler found for stream ID: {StreamId}", streamId);
        return;
      }

      _logger.LogInformation(
        "Setting desktop preview stream for stream ID: {StreamId}",
        streamId);

      // Set the stream on the signaler - this will trigger the ReadySignal
      signaler.SetStream(jpegChunks, Context.ConnectionId);
      
      using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
      // Wait for the stream to be fully consumed
      await signaler.EndSignal.Wait(cts.Token);

      _logger.LogInformation("Desktop preview stream completed for stream ID: {StreamId}", streamId);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while receiving desktop preview stream for stream ID: {StreamId}", streamId);
    }
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
      if (_appOptions.Value.AllowAgentsToSelfBootstrap && deviceDto.TenantId == Guid.Empty)
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

      deviceDto = UpdateDeviceDtoState(deviceDto);

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

    // Invalidate the device grid cache using the extension method
    await _outputCacheStore.InvalidateDeviceCacheAsync(device.Id);
    _logger.LogDebug("Invalidated device grid cache after device update: {DeviceId}", device.Id);

    if (device.Tags is null)
    {
      return;
    }

    var groupNames = device.Tags.Select(x => HubGroupNames.GetTagGroupName(x.Id, x.TenantId));
    await _viewerHub.Clients.Groups(groupNames).ReceiveDeviceUpdate(dto);
  }
  private async Task<Result<Device>> UpdateDeviceEntity(DeviceDto dto)
  {
    // Allow agents to self-bootstrap when enabled
    if (_appOptions.Value.AllowAgentsToSelfBootstrap)
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

  private DeviceDto UpdateDeviceDtoState(DeviceDto deviceDto)
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

  public async Task SendFileDownloadStream(Guid streamId, IAsyncEnumerable<byte[]> stream)
  {
    try
    {
      if (!_hubStreamStore.TryGet(streamId, out var signaler))
      {
        _logger.LogWarning("No signaler found for file download stream ID: {StreamId}", streamId);
        return;
      }

      _logger.LogInformation("Setting file download stream for stream ID: {StreamId}", streamId);
      
      // Set the stream on the signaler - this will trigger the ReadySignal
      signaler.SetStream(stream, Context.ConnectionId);
      
      using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(30));
      // Wait for the stream to be fully consumed
      await signaler.EndSignal.Wait(cts.Token);

      _logger.LogInformation("File download stream completed for stream ID: {StreamId}", streamId);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error handling file download stream {StreamId}", streamId);
      throw;
    }
  }

  public async IAsyncEnumerable<byte[]> GetFileUploadStream(FileUploadHubDto dto)
  {
    if (!_hubStreamStore.TryGet(dto.StreamId, out var signaler))
    {
      yield break;
    }
    try
    {

      _logger.LogInformation("Receiving file upload stream for: {FileName}", dto.FileName);

      using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
      // Wait for controller to set the stream
      await signaler.ReadySignal.Wait(cts.Token);

      Guard.IsNotNull(signaler.Stream);

      // Stream the file data
      await foreach (var chunk in signaler.Stream)
      {
        yield return chunk;
      }
    }
    finally
    {
      signaler.EndSignal.Set();
    }
  }
}