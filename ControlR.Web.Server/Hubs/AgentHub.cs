using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using ControlR.Libraries.Shared.Dtos.HubDtos;
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

  public ChannelReader<byte[]> GetFileStreamFromViewer(FileUploadHubDto dto)
  {
    if (!_hubStreamStore.TryGet<byte[]>(dto.StreamId, out var signaler))
    {
      _logger.LogWarning("No signaler found for file upload stream ID: {StreamId}", dto.StreamId);
      var errorChannel = Channel.CreateUnbounded<byte[]>();
      errorChannel.Writer.TryComplete(new InvalidOperationException("No signaler found for stream."));
      return errorChannel.Reader;
    }

    _logger.LogInformation("Agent is starting to read file upload stream for: {FileName}", dto.FileName);

    // Create a background task to log completion
    _ = Task.Run(async () =>
    {
      try
      {
        await signaler.Reader.Completion;
        _logger.LogInformation("Agent has finished reading file upload stream for: {FileName}", dto.FileName);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error reading file upload stream for: {FileName}", dto.FileName);
      }
    });

    return signaler.Reader;
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

  public async Task<bool> SendChatResponse(ChatResponseHubDto responseDto)
  {
    try
    {
      _logger.LogInformation(
        "Sending chat response to viewer {ViewerConnectionId} for session {SessionId}",
        responseDto.ViewerConnectionId,
        responseDto.SessionId);

      return await _viewerHub.Clients
        .Client(responseDto.ViewerConnectionId)
        .ReceiveChatResponse(responseDto);
    }
    catch (IOException ex) when (ex.Message.Contains("does not exist"))
    {
      _logger.LogWarning(
        "Viewer {ViewerConnectionId} for chat session {SessionId} is no longer connected.",
        responseDto.ViewerConnectionId,
        responseDto.SessionId);
      await Clients.Caller.CloseChatSession(responseDto.SessionId, responseDto.DesktopUiProcessId);
      return false;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error forwarding chat response to viewer.");
      return false;
    }
  }

  public async Task SendDesktopClientDownloadProgress(DesktopClientDownloadProgressDto progressDto)
  {
    await _viewerHub.Clients
      .Client(progressDto.ViewerConnectionId)
      .ReceiveDesktopClientDownloadProgress(progressDto);
  }

  public async Task SendDesktopPreviewStream(Guid streamId, ChannelReader<byte[]> jpegChunks)
  {
    try
    {
      _logger.LogInformation("Receiving desktop preview stream for stream ID: {StreamId}", streamId);

      await ProcessAgentStream(
        streamId,
        jpegChunks,
        async signaler => await signaler.WriteFromChannelReader(jpegChunks, Context.ConnectionAborted),
        "desktop preview");

      _logger.LogInformation("Desktop preview stream completed for stream ID: {StreamId}", streamId);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while receiving desktop preview stream for stream ID: {StreamId}", streamId);
    }
  }

  public async Task SendDirectoryContentsStream(Guid streamId, bool directoryExists,
    ChannelReader<FileSystemEntryDto[]> entryChunks)
  {
    try
    {
      await ProcessAgentStream(
        streamId,
        entryChunks,
        async signaler =>
        {
          signaler.Metadata = directoryExists;
          await signaler.WriteFromChannelReader(entryChunks, Context.ConnectionAborted);
        },
        "directory contents");
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error handling directory contents stream {StreamId}", streamId);
    }
  }

  public async Task<Result> SendFileDownloadStream(Guid streamId, ChannelReader<byte[]> stream)
  {
    try
    {
      _logger.LogInformation("Setting file download stream for stream ID: {StreamId}", streamId);

      await ProcessAgentStream(
        streamId,
        stream,
        async signaler => await signaler.WriteFromChannelReader(stream, Context.ConnectionAborted),
        "file download");

      _logger.LogInformation("File download stream completed for stream ID: {StreamId}", streamId);
      return Result.Ok();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error handling file download stream {StreamId}", streamId);
      return Result.Fail("An error occurred while handling the file download stream.");
    }
  }

  public async Task SendSubdirectoriesStream(Guid streamId, ChannelReader<FileSystemEntryDto[]> subdirectoryChunks)
  {
    try
    {
      await ProcessAgentStream(
        streamId,
        subdirectoryChunks,
        async signaler => await signaler.WriteFromChannelReader(subdirectoryChunks, Context.ConnectionAborted),
        "subdirectories");
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error handling subdirectories stream {StreamId}", streamId);
      throw;
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

  private static async Task DrainChannelReader<T>(ChannelReader<T> reader)
  {
    try
    {
      // Consume any remaining items in the channel to prevent SignalR streaming errors
      await foreach (var _ in reader.ReadAllAsync())
      {
        // Discard the data
      }
    }
    catch
    {
      // Ignore errors while draining
    }
  }

  private async Task AddToGroups(Device deviceEntity)
  {
    if (Device is not null)
    {
      return;
    }

    await Groups.AddToGroupAsync(Context.ConnectionId, HubGroupNames.GetTenantDevicesGroupName(deviceEntity.TenantId));
    await Groups.AddToGroupAsync(Context.ConnectionId,
      HubGroupNames.GetDeviceGroupName(deviceEntity.Id, deviceEntity.TenantId));
    if (deviceEntity.Tags is { Count: > 0 } tags)
    {
      foreach (var tag in tags)
      {
        await Groups.AddToGroupAsync(Context.ConnectionId,
          HubGroupNames.GetTagGroupName(tag.Id, deviceEntity.TenantId));
      }
    }
  }

  /// <summary>
  ///   Safely processes a streaming request by writing from an agent's ChannelReader to a signaler.
  ///   Automatically drains the channel on any error or cancellation to prevent SignalR connection breaks.
  /// </summary>
  private async Task ProcessAgentStream<T>(
    Guid streamId,
    ChannelReader<T> agentStream,
    Func<HubStreamSignaler<T>, Task> processSignaler,
    string streamType,
    [CallerMemberName] string callerName = "")
  {
    try
    {
      if (!_hubStreamStore.TryGet<T>(streamId, out var signaler))
      {
        _logger.LogWarning("No signaler found for {StreamType} stream ID: {StreamId}", streamType, streamId);
        await DrainChannelReader(agentStream);
        throw new InvalidOperationException($"No signaler found for {streamType} stream.");
      }

      await processSignaler(signaler);
    }
    catch (OperationCanceledException)
    {
      _logger.LogInformation("{StreamType} stream {StreamId} was canceled in method {CallerName}. ",
        streamType, streamId, callerName);

      await DrainChannelReader(agentStream);
    }
    catch (Exception)
    {
      await DrainChannelReader(agentStream);
      throw;
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

  private async Task<Result<Device>> UpdateDeviceEntity(DeviceDto dto)
  {
    // Allow agents to self-bootstrap when enabled
    if (_appOptions.Value.AllowAgentsToSelfBootstrap)
    {
      var device = await _deviceManager.AddOrUpdate(dto);
      return Result.Ok(device);
    }

    var updateResult = await _deviceManager.UpdateDevice(dto);
    if (!updateResult.IsSuccess)
    {
      await Clients.Caller.UninstallAgent(updateResult.Reason);
    }

    return updateResult;
  }
}