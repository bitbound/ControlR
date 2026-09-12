using System.Security.Claims;
using ControlR.Libraries.Api.Contracts.Dtos.HubDtos;
using ControlR.Libraries.Api.Contracts.Hubs.Clients;
using Microsoft.AspNetCore.SignalR;

namespace ControlR.Web.Server.Services.DeviceFileSystem;

/// <summary>
/// Runs the device file system operations that are carried out by asking a connected agent over the
/// hub. Every operation applies the same guards in the same order, namely load the device,
/// authorize the caller against it, require the agent to be online, then dispatch. What stopped an
/// operation is reported as a <see cref="FileSystemOutcome{TValue}" /> rather than as a response.
/// </summary>
/// <remarks>
/// The guard log levels and the log messages are deliberately left as they were found, and the
/// service never answers for the caller's status code. Endpoints that share a guard do not share a
/// response for it, and two of the write operations discard the agent's answer. Both belong to the
/// caller, which is why the service reports the agent's rejection for every operation even where
/// its caller ignores it today.
/// <para>
/// Every operation takes an optional <c>expectedTenantId</c>. A caller that has already resolved the
/// tenant it is acting for (the versioned API does) passes it, and the device load then carries an
/// explicit tenant predicate in addition to the device id. A caller that passes nothing relies on the
/// context's claims-driven query filter, which is how the internal endpoints have always worked.
/// </para>
/// </remarks>
public interface IDeviceFileSystemService
{
  /// <summary>
  /// Asks the agent to create a directory under <see cref="CreateDirectoryHubDto.ParentPath" />.
  /// The agent's answer is reported as <see cref="FileSystemFailure.HubRejected" />, which the
  /// creating endpoint has historically ignored.
  /// </summary>
  Task<FileSystemOutcome<object?>> CreateDirectory(
    ClaimsPrincipal user,
    Guid deviceId,
    InternalDtos.CreateDirectoryRequestDto request,
    CancellationToken cancellationToken,
    Guid? expectedTenantId = null);

  /// <summary>
  /// Asks the agent to delete a path. Only the path is forwarded; whether the caller described it as
  /// a directory is dropped at this boundary. As with directory creation, the agent's answer is
  /// reported and left for the caller to use or ignore.
  /// </summary>
  Task<FileSystemOutcome<object?>> DeletePath(
    ClaimsPrincipal user,
    Guid deviceId,
    InternalDtos.FileDeleteRequestDto request,
    CancellationToken cancellationToken,
    Guid? expectedTenantId = null);

  /// <summary>
  /// Asks the agent to stream a directory's entries and flattens what arrives before the stream
  /// closes. The agent's directory-exists signal travels in the stream's metadata.
  /// </summary>
  Task<FileSystemOutcome<InternalDtos.GetDirectoryContentsResponseDto>> GetDirectoryContents(
    ClaimsPrincipal user,
    InternalDtos.GetDirectoryContentsRequestDto request,
    CancellationToken cancellationToken,
    Guid? expectedTenantId = null);

  /// <summary>
  /// Asks the agent which log files it has on disk.
  /// </summary>
  Task<FileSystemOutcome<InternalDtos.GetLogFilesResponseDto>> GetLogFiles(
    ClaimsPrincipal user,
    Guid deviceId,
    CancellationToken cancellationToken,
    Guid? expectedTenantId = null);

  /// <summary>
  /// Asks the agent to split a path into its segments. An agent that answers with nothing at all is
  /// reported as <see cref="FileSystemFailure.HubRejected" /> with no reason.
  /// </summary>
  Task<FileSystemOutcome<InternalDtos.PathSegmentsResponseDto>> GetPathSegments(
    ClaimsPrincipal user,
    InternalDtos.GetPathSegmentsRequestDto request,
    CancellationToken cancellationToken,
    Guid? expectedTenantId = null);

  /// <summary>
  /// Asks the agent for the file system entries at its roots. The request is forwarded verbatim.
  /// </summary>
  Task<FileSystemOutcome<InternalDtos.GetRootDrivesResponseDto>> GetRootDrives(
    ClaimsPrincipal user,
    InternalDtos.GetRootDrivesRequestDto request,
    CancellationToken cancellationToken,
    Guid? expectedTenantId = null);

  /// <summary>
  /// Asks the agent to stream the subdirectories of a directory and flattens what arrives before
  /// the stream closes.
  /// </summary>
  Task<FileSystemOutcome<InternalDtos.GetSubdirectoriesResponseDto>> GetSubdirectories(
    ClaimsPrincipal user,
    InternalDtos.GetSubdirectoriesRequestDto request,
    CancellationToken cancellationToken,
    Guid? expectedTenantId = null);

  /// <summary>
  /// Asks the agent whether a directory and file name combine into a usable path. The agent's answer
  /// is returned as-is, including an answer that the path is invalid.
  /// </summary>
  Task<FileSystemOutcome<InternalDtos.ValidateFilePathResponseDto>> ValidateFilePath(
    ClaimsPrincipal user,
    Guid deviceId,
    InternalDtos.ValidateFilePathRequestDto request,
    CancellationToken cancellationToken,
    Guid? expectedTenantId = null);
}

public class DeviceFileSystemService(
  AppDb appDb,
  IHubContext<AgentHub, IAgentHubClient> agentHub,
  IHubStreamStore hubStreamStore,
  IAuthorizationService authorizationService,
  ILogger<DeviceFileSystemService> logger) : IDeviceFileSystemService
{
  private readonly IHubContext<AgentHub, IAgentHubClient> _agentHub = agentHub;
  private readonly AppDb _appDb = appDb;
  private readonly IAuthorizationService _authorizationService = authorizationService;
  private readonly IHubStreamStore _hubStreamStore = hubStreamStore;
  private readonly ILogger<DeviceFileSystemService> _logger = logger;

  public async Task<FileSystemOutcome<object?>> CreateDirectory(
    ClaimsPrincipal user,
    Guid deviceId,
    InternalDtos.CreateDirectoryRequestDto request,
    CancellationToken cancellationToken,
    Guid? expectedTenantId = null)
  {
    var guarded = await Guard(
      user,
      deviceId,
      DeviceResourcePolicies.FileSystemWrite,
      expectedTenantId,
      cancellationToken);

    if (guarded is not { Succeeded: true, Value: { } device })
    {
      return new(guarded.Failure, guarded.Reason, null);
    }

    var createDirectoryRequest = new CreateDirectoryHubDto(request.ParentPath, request.DirectoryName);

    try
    {
      var result = await _agentHub.Clients
        .Client(device.ConnectionId)
        .CreateDirectory(createDirectoryRequest);

      _logger.LogInformation("Directory creation requested for {DirectoryName} in {ParentPath} on device {DeviceId}",
        request.DirectoryName, request.ParentPath, deviceId);

      // An agent that is no longer reachable answers with nothing, which used to be indistinguishable
      // from success here. Report it as a rejection without letting the missing answer throw.
      if (result is null || !result.IsSuccess)
      {
        return new(FileSystemFailure.HubRejected, result?.Reason, null);
      }

      return new(FileSystemFailure.None, null, null);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error creating directory {DirectoryName} in {ParentPath} on device {DeviceId}",
        request.DirectoryName, request.ParentPath, deviceId);
      return new(FileSystemFailure.Unexpected, ex.Message, null);
    }
  }

  public async Task<FileSystemOutcome<object?>> DeletePath(
    ClaimsPrincipal user,
    Guid deviceId,
    InternalDtos.FileDeleteRequestDto request,
    CancellationToken cancellationToken,
    Guid? expectedTenantId = null)
  {
    var guarded = await Guard(
      user,
      deviceId,
      DeviceResourcePolicies.FileSystemDelete,
      expectedTenantId,
      cancellationToken);

    if (guarded is not { Succeeded: true, Value: { } device })
    {
      return new(guarded.Failure, guarded.Reason, null);
    }

    var deleteRequest = new FileDeleteHubDto(request.FilePath);

    try
    {
      var result = await _agentHub.Clients
        .Client(device.ConnectionId)
        .DeleteFile(deleteRequest);

      _logger.LogInformation("File deletion requested for {FilePath} on device {DeviceId}",
        request.FilePath, deviceId);

      // See CreateDirectory: the missing answer of an unreachable agent is a rejection, and the
      // deleting endpoint is free to ignore it.
      if (result is null || !result.IsSuccess)
      {
        return new(FileSystemFailure.HubRejected, result?.Reason, null);
      }

      return new(FileSystemFailure.None, null, null);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error deleting file {FilePath} on device {DeviceId}",
        request.FilePath, deviceId);
      return new(FileSystemFailure.Unexpected, ex.Message, null);
    }
  }

  public async Task<FileSystemOutcome<InternalDtos.GetDirectoryContentsResponseDto>> GetDirectoryContents(
    ClaimsPrincipal user,
    InternalDtos.GetDirectoryContentsRequestDto request,
    CancellationToken cancellationToken,
    Guid? expectedTenantId = null)
  {
    var guarded = await Guard(
      user,
      request.DeviceId,
      DeviceResourcePolicies.FileSystemRead,
      expectedTenantId,
      cancellationToken);

    if (guarded is not { Succeeded: true, Value: { } device })
    {
      return new(guarded.Failure, guarded.Reason, null);
    }

    try
    {
      var streamed = await StreamEntries(
        device,
        "directory contents",
        request.DirectoryPath,
        streamId => _agentHub.Clients
          .Client(device.ConnectionId)
          .StreamDirectoryContents(
            new DirectoryContentsStreamRequestHubDto(streamId, request.DeviceId, request.DirectoryPath)),
        cancellationToken);

      if (streamed is not { Succeeded: true, Value: { } entries })
      {
        return new(streamed.Failure, streamed.Reason, null);
      }

      return new(FileSystemFailure.None, null, new InternalDtos.GetDirectoryContentsResponseDto(
        [.. entries.Items], entries.DirectoryExists));
    }
    catch (OperationCanceledException)
    {
      _logger.LogWarning("Directory contents stream canceled/timed out for device {DeviceId} path {DirectoryPath}", request.DeviceId, request.DirectoryPath);
      return new(FileSystemFailure.Cancelled, null, null);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while streaming directory contents for device {DeviceId} path {DirectoryPath}", request.DeviceId, request.DirectoryPath);
      return new(FileSystemFailure.Unexpected, ex.Message, null);
    }
  }

  public async Task<FileSystemOutcome<InternalDtos.GetLogFilesResponseDto>> GetLogFiles(
    ClaimsPrincipal user,
    Guid deviceId,
    CancellationToken cancellationToken,
    Guid? expectedTenantId = null)
  {
    var guarded = await Guard(
      user,
      deviceId,
      DeviceResourcePolicies.LogsRead,
      expectedTenantId,
      cancellationToken);

    if (guarded is not { Succeeded: true, Value: { } device })
    {
      return new(guarded.Failure, guarded.Reason, null);
    }

    try
    {
      var result = await _agentHub
        .Clients
        .Client(device.ConnectionId)
        .GetLogFiles();

      if (!result.IsSuccess)
      {
        _logger.LogError("Get log files request failed for device {DeviceId}: {Reason}",
          deviceId, result.Reason);
        return new(FileSystemFailure.HubRejected, result.Reason, null);
      }

      return new(FileSystemFailure.None, null, result.Value);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error getting log files from device {DeviceId}", deviceId);
      return new(FileSystemFailure.Unexpected, ex.Message, null);
    }
  }

  public async Task<FileSystemOutcome<InternalDtos.PathSegmentsResponseDto>> GetPathSegments(
    ClaimsPrincipal user,
    InternalDtos.GetPathSegmentsRequestDto request,
    CancellationToken cancellationToken,
    Guid? expectedTenantId = null)
  {
    // This operation stays off Guard. Lifting its guards out of the try would move device-load and
    // authorization failures out of this catch's view, and its guards log differently from the shared
    // ones: another message for a missing device, none at all for a rejected authorization.
    try
    {
      var device = await LoadDevice(request.DeviceId, expectedTenantId, cancellationToken);

      if (device is null)
      {
        _logger.LogWarning("Device not found for path segments request: {DeviceId}", request.DeviceId);
        return new(FileSystemFailure.DeviceNotFound, null, null);
      }

      var authResult = await _authorizationService.AuthorizeAsync(
        user,
        device,
        DeviceResourcePolicies.FileSystemRead);
      if (!authResult.Succeeded)
      {
        return new(FileSystemFailure.Forbidden, null, null);
      }

      if (!device.IsOnline)
      {
        _logger.LogWarning("Device {DeviceId} is not online.", request.DeviceId);
        return new(FileSystemFailure.DeviceOffline, null, null);
      }

      _logger.LogInformation("Getting path segments for device {DeviceId} path {TargetPath}", request.DeviceId, request.TargetPath);

      var hubDto = new GetPathSegmentsHubDto { TargetPath = request.TargetPath };
      var result = await _agentHub.Clients
        .Client(device.ConnectionId)
        .GetPathSegments(hubDto);

      if (result is null)
      {
        _logger.LogWarning("No response received from agent for path segments request on device {DeviceId} path {TargetPath}", request.DeviceId, request.TargetPath);
        return new(FileSystemFailure.HubRejected, null, null);
      }

      return new(FileSystemFailure.None, null, result);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while getting path segments for device {DeviceId} path {TargetPath}", request.DeviceId, request.TargetPath);
      return new(FileSystemFailure.Unexpected, ex.Message, null);
    }
  }

  public async Task<FileSystemOutcome<InternalDtos.GetRootDrivesResponseDto>> GetRootDrives(
    ClaimsPrincipal user,
    InternalDtos.GetRootDrivesRequestDto request,
    CancellationToken cancellationToken,
    Guid? expectedTenantId = null)
  {
    var guarded = await Guard(
      user,
      request.DeviceId,
      DeviceResourcePolicies.FileSystemRead,
      expectedTenantId,
      cancellationToken);

    if (guarded is not { Succeeded: true, Value: { } device })
    {
      return new(guarded.Failure, guarded.Reason, null);
    }

    try
    {
      var result = await _agentHub.Clients.Client(device.ConnectionId)
        .GetRootDrives(request);

      if (result.IsSuccess)
      {
        return new(FileSystemFailure.None, null, result.Value);
      }

      _logger.LogWarning("Failed to get root drives for device {DeviceId}: {Reason}",
        request.DeviceId, result.Reason);
      return new(FileSystemFailure.HubRejected, result.Reason, null);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while getting root drives for device {DeviceId}", request.DeviceId);
      return new(FileSystemFailure.Unexpected, ex.Message, null);
    }
  }

  public async Task<FileSystemOutcome<InternalDtos.GetSubdirectoriesResponseDto>> GetSubdirectories(
    ClaimsPrincipal user,
    InternalDtos.GetSubdirectoriesRequestDto request,
    CancellationToken cancellationToken,
    Guid? expectedTenantId = null)
  {
    var guarded = await Guard(
      user,
      request.DeviceId,
      DeviceResourcePolicies.FileSystemRead,
      expectedTenantId,
      cancellationToken);

    if (guarded is not { Succeeded: true, Value: { } device })
    {
      return new(guarded.Failure, guarded.Reason, null);
    }

    try
    {
      var streamed = await StreamEntries(
        device,
        "subdirectories",
        request.DirectoryPath,
        streamId => _agentHub.Clients
          .Client(device.ConnectionId)
          .StreamSubdirectories(
            new SubdirectoriesStreamRequestHubDto(streamId, request.DeviceId, request.DirectoryPath)),
        cancellationToken);

      if (streamed is not { Succeeded: true, Value: { } entries })
      {
        return new(streamed.Failure, streamed.Reason, null);
      }

      return new(FileSystemFailure.None, null, new InternalDtos.GetSubdirectoriesResponseDto(entries.Items.ToArray()));
    }
    catch (OperationCanceledException)
    {
      _logger.LogWarning("Subdirectories stream canceled/timed out for device {DeviceId} path {DirectoryPath}", request.DeviceId, request.DirectoryPath);
      return new(FileSystemFailure.Cancelled, null, null);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while streaming subdirectories for device {DeviceId} path {DirectoryPath}", request.DeviceId, request.DirectoryPath);
      return new(FileSystemFailure.Unexpected, ex.Message, null);
    }
  }

  public async Task<FileSystemOutcome<InternalDtos.ValidateFilePathResponseDto>> ValidateFilePath(
    ClaimsPrincipal user,
    Guid deviceId,
    InternalDtos.ValidateFilePathRequestDto request,
    CancellationToken cancellationToken,
    Guid? expectedTenantId = null)
  {
    var guarded = await Guard(
      user,
      deviceId,
      DeviceResourcePolicies.FileSystemRead,
      expectedTenantId,
      cancellationToken);

    if (guarded is not { Succeeded: true, Value: { } device })
    {
      return new(guarded.Failure, guarded.Reason, null);
    }

    var validateRequest = new ValidateFilePathHubDto(request.DirectoryPath, request.FileName);

    try
    {
      var result = await _agentHub.Clients
        .Client(device.ConnectionId)
        .ValidateFilePath(validateRequest);

      _logger.LogInformation(
        "File path validation completed for {FileName} in {DirectoryPath} on device {DeviceId}: {IsValid}",
        request.FileName, request.DirectoryPath, deviceId, result.IsValid);

      return new(FileSystemFailure.None, null, result);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error validating file path {FileName} in {DirectoryPath} on device {DeviceId}",
        request.FileName, request.DirectoryPath, deviceId);
      return new(FileSystemFailure.Unexpected, ex.Message, null);
    }
  }

  /// <summary>
  /// Loads the device, applies the operation's device resource policy to the caller, and requires the
  /// agent to be connected. On success the value is the device to dispatch to; on failure the outcome
  /// carries the same condition the operation used to check inline, which is what the caller reports.
  /// </summary>
  private async Task<FileSystemOutcome<Device>> Guard(
    ClaimsPrincipal user,
    Guid deviceId,
    string policyName,
    Guid? expectedTenantId,
    CancellationToken cancellationToken)
  {
    var device = await LoadDevice(deviceId, expectedTenantId, cancellationToken);

    if (device is null)
    {
      _logger.LogWarning("Device {DeviceId} not found.", deviceId);
      return new(FileSystemFailure.DeviceNotFound, null, null);
    }

    var authResult = await _authorizationService.AuthorizeAsync(user, device, policyName);

    if (!authResult.Succeeded)
    {
      _logger.LogCritical("Authorization failed for user {UserName} on device {DeviceId}.",
        user.Identity?.Name, deviceId);
      return new(FileSystemFailure.Forbidden, null, null);
    }

    if (!device.IsOnline)
    {
      _logger.LogWarning("Device {DeviceId} is not online.", deviceId);
      return new(FileSystemFailure.DeviceOffline, null, null);
    }

    return new(FileSystemFailure.None, null, device);
  }

  /// <summary>
  /// Loads the target device by id. When <paramref name="expectedTenantId"/> is supplied it joins the
  /// query as an explicit tenant predicate, so a caller that resolved the tenant before dispatching
  /// cannot be handed another tenant's device even in a context whose global query filter is inactive.
  /// A caller that passes nothing keeps relying on the claims-driven filter, as the internal endpoints
  /// always have.
  /// </summary>
  private async Task<Device?> LoadDevice(
    Guid deviceId,
    Guid? expectedTenantId,
    CancellationToken cancellationToken)
  {
    var query = _appDb.Devices
      .AsNoTracking()
      .Where(x => x.Id == deviceId);

    if (expectedTenantId is Guid tenantId)
    {
      query = query.Where(x => x.TenantId == tenantId);
    }

    return await query.FirstOrDefaultAsync(cancellationToken);
  }

  /// <summary>
  /// Opens a stream session, asks the agent to fill it, and collects what arrives before the agent
  /// closes it. Used by both streaming read operations, which differ only in what they do with the
  /// directory-exists signal afterwards.
  /// </summary>
  private async Task<FileSystemOutcome<StreamedEntries>> StreamEntries(
    Device device,
    string operationName,
    string directoryPath,
    Func<Guid, Task<HubResult>> startStream,
    CancellationToken cancellationToken)
  {
    var streamId = Guid.NewGuid();
    using var signaler = _hubStreamStore.GetOrCreate<InternalDtos.FileSystemEntryDto[]>(streamId);

    var result = await startStream(streamId);

    if (!result.IsSuccess)
    {
      _logger.LogWarning("Failed to initiate {OperationName} stream for device {DeviceId} path {DirectoryPath}: {Reason}",
        operationName, device.Id, directoryPath, result.Reason);
      return new(FileSystemFailure.HubRejected, result.Reason, null);
    }

    var items = new List<InternalDtos.FileSystemEntryDto>();
    await foreach (var chunk in signaler.Reader.ReadAllAsync(cancellationToken))
    {
      items.AddRange(chunk);
    }

    return new(FileSystemFailure.None, null, new StreamedEntries(items, signaler.Metadata is bool exists && exists));
  }

  /// <summary>
  /// What a streamed read operation collected: the entries in arrival order, and the directory-exists
  /// signal the agent left in the stream session's metadata. The operation that never looked at that
  /// signal keeps ignoring it.
  /// </summary>
  private sealed record StreamedEntries(List<InternalDtos.FileSystemEntryDto> Items, bool DirectoryExists);
}
