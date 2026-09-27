using System.Runtime.CompilerServices;
using System.Threading.Channels;
using ControlR.Libraries.Api.Contracts.Dtos.HubDtos;
using ControlR.Libraries.Api.Contracts.Hubs.Clients;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.SignalR;
using ControlR.Web.Server.Services.DeviceManagement;
using ControlR.Libraries.Shared.Services.Encryption;
using ControlR.Web.Server.Extensions.Dtos.Internal;

namespace ControlR.Web.Server.Hubs;

public class AgentHub(
  AppDb appDb,
  TimeProvider timeProvider,
  IHubContext<ViewerHub, IViewerHubClient> viewerHub,
  IDeviceManager deviceManager,
  IOutputCacheStore outputCacheStore,
  IHubStreamStore hubStreamStore,
  IAgentVersionProvider agentVersionProvider,
  IOptions<AppOptions> appOptions,
  IOptions<DeveloperOptions> developerOptions,
  IOptions<ServerLifecycleOptions> serverOptions,
  IEd25519KeyProvider keyProvider,
  ILogger<AgentHub> logger) : HubWithItems<IAgentHubClient>, IAgentHub
{
  private readonly IAgentVersionProvider _agentVersionProvider = agentVersionProvider;
  private readonly AppDb _appDb = appDb;
  private readonly IOptions<AppOptions> _appOptions = appOptions;
  private readonly IOptions<DeveloperOptions> _developerOptions = developerOptions;
  private readonly IDeviceManager _deviceManager = deviceManager;
  private readonly IHubStreamStore _hubStreamStore = hubStreamStore;
  private readonly IEd25519KeyProvider _keyProvider = keyProvider;
  private readonly ILogger<AgentHub> _logger = logger;
  private readonly IOutputCacheStore _outputCacheStore = outputCacheStore;
  private readonly IOptions<ServerLifecycleOptions> _serverOptions = serverOptions;
  private readonly TimeProvider _timeProvider = timeProvider;
  private readonly IHubContext<ViewerHub, IViewerHubClient> _viewerHub = viewerHub;

  private InternalDtos.DeviceResponseDto? Device
  {
    get => GetItem<InternalDtos.DeviceResponseDto?>(null);
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
          var updateResult = await _deviceManager.MarkDeviceOffline(cachedDeviceDto.Id, _timeProvider.GetLocalNow());
          if (updateResult.IsSuccess)
          {
            var offlineDto = cachedDeviceDto with
            {
              IsOnline = false,
              ConnectionId = string.Empty,
              LastSeen = _timeProvider.GetLocalNow()
            };
            await SendDeviceUpdate(updateResult.Value, offlineDto);
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
      await Clients.Caller.CloseChatSession(responseDto.SessionId, responseDto.DesktopSessionProcessId);
      return false;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error forwarding chat response to viewer.");
      return false;
    }
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
    ChannelReader<InternalDtos.FileSystemEntryDto[]> entryChunks)
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

  public async Task<HubResult> SendFileContentStream(Guid streamId, ChannelReader<byte[]> stream)
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
      return HubResult.Ok();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error handling file download stream {StreamId}", streamId);
      return HubResult.Fail("An error occurred while handling the file download stream.");
    }
  }

  public async Task SendSubdirectoriesStream(Guid streamId, ChannelReader<InternalDtos.FileSystemEntryDto[]> subdirectoryChunks)
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

  public async Task<HubResult<InternalDtos.DeviceResponseDto>> UpdateDeviceSigned(SignedDto<DeviceUpdateRequestDto> signedDto)
  {
    try
    {
      var agentDto = signedDto.Dto;
      var allowSelfBootstrap = _developerOptions.Value.AllowAgentsToSelfBootstrap;

      // Trust only the key stored for a known device. A caller-supplied key is accepted only
      // for a device the server has never seen, with self-bootstrap enabled.
      var device = await _appDb.Devices
        .IgnoreQueryFilters()
        .FirstOrDefaultAsync(x => x.Id == agentDto.Id);
      var storedPublicKey = device?.PublicKey;
      var isUnknownDevice = device is null;

      if (isUnknownDevice && !allowSelfBootstrap)
      {
        _logger.LogWarning(
          "Rejecting update from unknown device {DeviceId}. Self-bootstrap is disabled.",
          agentDto.Id);
        return HubResult.Fail<InternalDtos.DeviceResponseDto>("Unknown device.");
      }

      if (!isUnknownDevice && string.IsNullOrEmpty(storedPublicKey))
      {
        _logger.LogWarning(
          "Rejecting update from keyless device {DeviceId}. The device must be enrolled with an installer key.",
          agentDto.Id);
        return HubResult.Fail<InternalDtos.DeviceResponseDto>("Device requires enrollment.");
      }

      var publicKeyBase64 = !string.IsNullOrEmpty(storedPublicKey)
        ? storedPublicKey
        : signedDto.PublicKey;

      var keyValidationResult = _keyProvider.ValidatePublicKeyBase64(publicKeyBase64);
      if (!keyValidationResult.IsSuccess)
      {
        _logger.LogWarning(
          "Public key validation failed for device {DeviceId}: {Reason}",
          agentDto.Id,
          keyValidationResult.Reason);
        return HubResult.Fail<InternalDtos.DeviceResponseDto>(keyValidationResult.Reason);
      }

      var publicKeyBytes = keyValidationResult.Value;

      if (!_keyProvider.Verify(signedDto, publicKeyBytes))
      {
        _logger.LogWarning(
          "Signature verification failed. Device Id: {DeviceId}. Device Name: {DeviceName}", 
          agentDto.Id,
          agentDto.Name);
          
        return HubResult.Fail<InternalDtos.DeviceResponseDto>("Signature verification failed.");
      }

      // Disabled when AgentClockSkewTolerance is null.
      var clockSkew = _appOptions.Value.AgentClockSkewTolerance;
      if (clockSkew.HasValue && !_keyProvider.VerifyTimestamp(signedDto, clockSkew.Value))
      {
        _logger.LogWarning(
          "Timestamp expired for device {DeviceId}. " + 
          "Are system clocks synchronized on both the server and the device?", 
          agentDto.Id);
        return HubResult.Fail<InternalDtos.DeviceResponseDto>("Timestamp expired.");
      }

      // Handle decommissioning if enabled.
      if (_serverOptions.Value.DecommissionServer)
      {
        return await HandleAgentUpdateForDecommission(agentDto, device);
      }

      // Developer-only self-bootstrap. Take the tenant from the server, never the caller, and
      // require exactly one tenant. Multi-tenant servers must use installer keys.
      if (allowSelfBootstrap)
      {
        if (device is null || device.TenantId == Guid.Empty)
        {
          var tenants = await _appDb.Tenants
            .OrderByDescending(x => x.CreatedAt)
            .Take(2)
            .ToListAsync();

          if (tenants.Count == 0)
          {
            return HubResult.Fail<InternalDtos.DeviceResponseDto>("No tenants found.");
          }

          if (tenants.Count > 1)
          {
            return HubResult.Fail<InternalDtos.DeviceResponseDto>(
              "Self-bootstrap is only allowed on single-tenant servers. Use an installer key instead.");
          }

          agentDto = agentDto with { TenantId = tenants[0].Id };
        }
        else
        {
          // Known device. Its tenant is immutable, so ignore the caller-supplied value.
          agentDto = agentDto with { TenantId = device.TenantId };
        }
      }

      if (agentDto.TenantId == Guid.Empty)
      {
        return HubResult.Fail<InternalDtos.DeviceResponseDto>("Invalid tenant ID.");
      }

      if (!await _appDb.Tenants.AnyAsync(x => x.Id == agentDto.TenantId))
      {
        return HubResult.Fail<InternalDtos.DeviceResponseDto>("Invalid tenant ID.");
      }

      var remoteIp = Context.GetHttpContext()?.Connection.RemoteIpAddress;
      var connectionContext = new DeviceConnectionContext(
        ConnectionId: Context.ConnectionId,
        RemoteIpAddress: remoteIp,
        LastSeen: _timeProvider.GetLocalNow(),
        IsOnline: true
      );

      var updateResult = allowSelfBootstrap
        ? await UpdateOrBootstrapDevice(agentDto, device, connectionContext, publicKeyBase64)
        : await UpdateKnownDevice(agentDto, connectionContext);

      if (!updateResult.IsSuccess)
      {
        return HubResult.Fail<InternalDtos.DeviceResponseDto>(updateResult.Reason);
      }

      var deviceEntity = updateResult.Value;

      var isOutdated = await GetIsAgentOutdated(deviceEntity);
      Device = deviceEntity.ToInternalResponseDto(isOutdated);

      await SendDeviceUpdate(deviceEntity, Device);

      return HubResult.Ok(Device);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while updating signed device.");
      return HubResult.Fail<InternalDtos.DeviceResponseDto>("An error occurred while updating the device.");
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

  private async Task<bool> GetIsAgentOutdated(Device deviceEntity)
  {
    var agentVersionResult = await _agentVersionProvider.TryGetAgentVersion();
    if (!agentVersionResult.IsSuccess)
    {
      return false;
    }

    if (!Version.TryParse(deviceEntity.AgentVersion, out var deviceVersion))
    {
      return false;
    }

    var currentAgentVersion = agentVersionResult.Value;
    return deviceVersion != currentAgentVersion;
  }

  private async Task<HubResult<InternalDtos.DeviceResponseDto>> HandleAgentUpdateForDecommission(
    DeviceUpdateRequestDto agentDto,
    Device? device = null)
  {
    _logger.LogInformation(
          "Rejecting device update from agent {AgentName} because server is decommissioned.",
          agentDto.Name);

    await Clients.Caller.UninstallAgent("Server has been decommissioned.");

    if (device is not null)
    {
      _appDb.Devices.Remove(device);
    }

    await _appDb.SaveChangesAsync();

    await _outputCacheStore.InvalidateDeviceCacheAsync(agentDto.Id);
    _logger.LogDebug("Invalidated device grid cache after device update: {DeviceId}", agentDto.Id);

    return HubResult.Fail<InternalDtos.DeviceResponseDto>("Server is decommissioned.");
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
      await processSignaler.Invoke(signaler);
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

  private async Task SendDeviceUpdate(Device device, InternalDtos.DeviceResponseDto dto)
  {
    await _viewerHub.Clients
      .Group(HubGroupNames.DeviceHeartbeat(device.Id))
      .ReceiveDeviceUpdate(dto);

    // Invalidate the device grid cache using the extension method.
    await _outputCacheStore.InvalidateDeviceCacheAsync(device.Id);
    _logger.LogDebug("Invalidated device grid cache after device update: {DeviceId}", device.Id);
  }

  /// <summary>
  /// Updates a device that is already enrolled with a stored public key. The stored key is
  /// never replaced.
  /// </summary>
  private async Task<HubResult<Device>> UpdateKnownDevice(
    DeviceUpdateRequestDto agentDto,
    DeviceConnectionContext context)
  {
    var updateResult = await _deviceManager.UpdateDevice(agentDto, context);
    if (updateResult.IsSuccess)
    {
      return HubResult.Ok(updateResult.Value);
    }

    return HubResult.Fail<Device>(updateResult.Reason);
  }

  /// <summary>
  /// Creates a device the server has never seen. Developer-only, reached only when
  /// self-bootstrap is enabled. The caller-supplied key is adopted here and nowhere else.
  /// </summary>
  private async Task<HubResult<Device>> UpdateOrBootstrapDevice(
    DeviceUpdateRequestDto agentDto,
    Device? existingDevice,
    DeviceConnectionContext context,
    string? newDevicePublicKey)
  {
    if (existingDevice is null)
    {
      var created = await _deviceManager.AddOrUpdate(agentDto, context, publicKeyBase64: newDevicePublicKey);
      return HubResult.Ok(created);
    }

    return await UpdateKnownDevice(agentDto, context);
  }
}
