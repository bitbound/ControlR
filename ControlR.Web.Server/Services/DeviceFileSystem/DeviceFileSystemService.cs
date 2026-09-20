using System.Security.Claims;
using System.Threading.Channels;
using ControlR.Libraries.Api.Contracts.Dtos.HubDtos;
using ControlR.Libraries.Api.Contracts.Hubs.Clients;
using Microsoft.AspNetCore.SignalR;

namespace ControlR.Web.Server.Services.DeviceFileSystem;

/// <summary>
/// Runs the device file system operations that ask a connected agent over the hub. Every operation
/// applies the same guards in the same order: load the device, authorize the caller against it,
/// require the agent to be online, then dispatch. What stopped an operation is reported as a
/// <see cref="FileSystemOutcome" /> rather than as a response.
/// </summary>
/// <remarks>
/// The service never chooses a status code; that belongs to the caller.
/// <para>
/// Every operation takes an optional <c>expectedTenantId</c>. A caller that already resolved its tenant
/// (the versioned API does) passes it, and the device load carries an explicit tenant predicate. A
/// caller that passes nothing relies on the context's claims-driven query filter.
/// </para>
/// </remarks>
public interface IDeviceFileSystemService
{

  /// <summary>
  /// Applies <see cref="UploadFile" />'s guards without starting a transfer, so a caller holding a
  /// request body can read it only after the device accepted the upload. UploadFile guards again, so
  /// it stays a complete operation on its own.
  /// </summary>
  Task<FileSystemOutcome> AuthorizeUpload(
    ClaimsPrincipal user,
    Guid deviceId,
    CancellationToken cancellationToken,
    Guid? expectedTenantId = null);

  /// <summary>
  /// Asks the agent to create a directory under <see cref="CreateDirectoryHubDto.ParentPath" />. The
  /// agent's own failure text is reported as <see cref="FileSystemFailure.RemoteFailure" />, and an
  /// agent that never answered is reported as <see cref="FileSystemFailure.NoResponse" />.
  /// </summary>
  Task<FileSystemOutcome> CreateDirectory(
    ClaimsPrincipal user,
    Guid deviceId,
    InternalDtos.CreateDirectoryRequestDto request,
    CancellationToken cancellationToken,
    Guid? expectedTenantId = null);

  /// <summary>
  /// Asks the agent to delete a path. Only the path is forwarded. The agent stats it and deletes a
  /// directory tree or a file accordingly. The agent's answer is reported, as with directory creation.
  /// </summary>
  Task<FileSystemOutcome> DeletePath(
    ClaimsPrincipal user,
    Guid deviceId,
    InternalDtos.DeletePathRequestDto request,
    CancellationToken cancellationToken,
    Guid? expectedTenantId = null);

  /// <summary>
  /// Asks the agent to stream a directory's entries and flattens what arrives before the stream closes.
  /// The agent's directory-exists signal travels in the stream's metadata.
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
  /// reported as <see cref="FileSystemFailure.NoResponse" />.
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
  /// Asks the agent to pack the requested paths into one archive and stream it back. The reported size
  /// is the agent's, not a count of what has arrived.
  /// </summary>
  Task<FileSystemOutcome<FileTransferSession>> StartArchiveDownload(
    ClaimsPrincipal user,
    Guid deviceId,
    InternalDtos.DownloadArchiveRequestDto request,
    CancellationToken cancellationToken,
    Guid? expectedTenantId = null);

  /// <summary>
  /// Asks the agent to stream one file back. The transfer carries the agent's own display name for the
  /// file, which is what the response's <c>Content-Disposition</c> should carry.
  /// </summary>
  Task<FileSystemOutcome<FileTransferSession>> StartFileDownload(
    ClaimsPrincipal user,
    Guid deviceId,
    string filePath,
    CancellationToken cancellationToken,
    Guid? expectedTenantId = null);

  /// <summary>
  /// Asks the agent to stream the contents of one log file back. A log file's length is unknown up
  /// front, so the session reports no size.
  /// </summary>
  Task<FileSystemOutcome<FileTransferSession>> StartLogFileContents(
    ClaimsPrincipal user,
    Guid deviceId,
    string filePath,
    CancellationToken cancellationToken,
    Guid? expectedTenantId = null);

  /// <summary>
  /// Pipes <paramref name="fileStream" /> to the agent, which writes it to disk. The bytes flow while
  /// the agent call is in flight rather than being buffered first.
  /// </summary>
  Task<FileSystemOutcome> UploadFile(
    ClaimsPrincipal user,
    Guid deviceId,
    Stream fileStream,
    string fileName,
    long fileLength,
    string targetSaveDirectory,
    bool overwrite,
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

  public async Task<FileSystemOutcome> AuthorizeUpload(
    ClaimsPrincipal user,
    Guid deviceId,
    CancellationToken cancellationToken,
    Guid? expectedTenantId = null)
  {
    var guarded = await Guard(
      user,
      deviceId,
      DeviceResourcePolicies.FileSystemTransferUpload,
      expectedTenantId,
      cancellationToken);

    return new(guarded.Failure, guarded.Reason);
  }

  public async Task<FileSystemOutcome> CreateDirectory(
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
      return new(guarded.Failure, guarded.Reason);
    }

    var createDirectoryRequest = new CreateDirectoryHubDto(request.ParentPath, request.DirectoryName);

    try
    {
      var result = await _agentHub.Clients
        .Client(device.ConnectionId)
        .CreateDirectory(createDirectoryRequest);

      _logger.LogInformation("Directory creation requested for {DirectoryName} in {ParentPath} on device {DeviceId}",
        request.DirectoryName, request.ParentPath, deviceId);

      if (result is null)
      {
        _logger.LogWarning("No response received from agent for directory creation on device {DeviceId}", deviceId);
        return new(FileSystemFailure.NoResponse, null);
      }

      if (!result.IsSuccess)
      {
        return new(FileSystemFailure.RemoteFailure, result.Reason);
      }

      return new(FileSystemFailure.None, null);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error creating directory {DirectoryName} in {ParentPath} on device {DeviceId}",
        request.DirectoryName, request.ParentPath, deviceId);
      return new(FileSystemFailure.Unexpected, ex.Message);
    }
  }

  public async Task<FileSystemOutcome> DeletePath(
    ClaimsPrincipal user,
    Guid deviceId,
    InternalDtos.DeletePathRequestDto request,
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
      return new(guarded.Failure, guarded.Reason);
    }

    var deleteRequest = new FileDeleteHubDto(request.FilePath);

    try
    {
      var result = await _agentHub.Clients
        .Client(device.ConnectionId)
        .DeleteFile(deleteRequest);

      _logger.LogInformation("Path deletion requested for {FilePath} on device {DeviceId}",
        request.FilePath, deviceId);

      if (result is null)
      {
        _logger.LogWarning("No response received from agent for path deletion on device {DeviceId}", deviceId);
        return new(FileSystemFailure.NoResponse, null);
      }

      if (!result.IsSuccess)
      {
        return new(FileSystemFailure.RemoteFailure, result.Reason);
      }

      return new(FileSystemFailure.None, null);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error deleting path {FilePath} on device {DeviceId}",
        request.FilePath, deviceId);
      return new(FileSystemFailure.Unexpected, ex.Message);
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

      if (result is null)
      {
        _logger.LogWarning("No response received from agent for log files request on device {DeviceId}", deviceId);
        return new(FileSystemFailure.NoResponse, null, null);
      }

      if (!result.IsSuccess)
      {
        _logger.LogError("Get log files request failed for device {DeviceId}: {Reason}",
          deviceId, result.Reason);
        return new(FileSystemFailure.RemoteFailure, result.Reason, null);
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
        return new(FileSystemFailure.NoResponse, null, null);
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

      if (result is null)
      {
        _logger.LogWarning("No response received from agent for root drives request on device {DeviceId}", request.DeviceId);
        return new(FileSystemFailure.NoResponse, null, null);
      }

      if (result.IsSuccess)
      {
        return new(FileSystemFailure.None, null, result.Value);
      }

      _logger.LogWarning("Failed to get root drives for device {DeviceId}: {Reason}",
        request.DeviceId, result.Reason);
      return new(FileSystemFailure.RemoteFailure, result.Reason, null);
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

  public async Task<FileSystemOutcome<FileTransferSession>> StartArchiveDownload(
    ClaimsPrincipal user,
    Guid deviceId,
    InternalDtos.DownloadArchiveRequestDto request,
    CancellationToken cancellationToken,
    Guid? expectedTenantId = null)
  {
    var guarded = await Guard(
      user,
      deviceId,
      DeviceResourcePolicies.FileSystemTransferDownload,
      expectedTenantId,
      cancellationToken);

    if (guarded is not { Succeeded: true, Value: { } device })
    {
      return new(guarded.Failure, guarded.Reason, null);
    }

    var streamId = Guid.NewGuid();
    var signaler = _hubStreamStore.GetOrCreate<byte[]>(streamId, HubStreamExpiration.FileTransfer);
    FileTransferSession? session = null;

    try
    {
      var downloadRequest = new FileArchiveDownloadHubDto(
        streamId,
        request.ArchiveFileName,
        [.. request.TargetPaths]);

      var result = await _agentHub.Clients
        .Client(device.ConnectionId)
        .UploadArchiveToViewer(downloadRequest);

      if (result is null)
      {
        _logger.LogWarning("No response received from agent for archive download on device {DeviceId}", deviceId);
        return new(FileSystemFailure.NoResponse, null, null);
      }

      if (!result.IsSuccess)
      {
        _logger.LogWarning("Archive download request failed for device {DeviceId}: {Reason}",
          deviceId, result.Reason);
        return new(FileSystemFailure.RemoteFailure, result.Reason, null);
      }

      _logger.LogInformation("Archive download started for device {DeviceId} with {ItemCount} item(s)",
        deviceId, request.TargetPaths.Count);

      session = new FileTransferSession(
        signaler,
        result.Value.FileDisplayName,
        result.Value.FileSize,
        cancellationToken);

      return new(FileSystemFailure.None, null, session);
    }
    catch (OperationCanceledException)
    {
      _logger.LogWarning("Archive download for device {DeviceId} was canceled.", deviceId);
      return new(FileSystemFailure.Cancelled, null, null);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error starting archive download from device {DeviceId}", deviceId);
      return new(FileSystemFailure.Unexpected, ex.Message, null);
    }
    finally
    {
      // The session owns the signaler once it is built. A path that returns without one has to
      // release it here, because no response body is coming to drain it.
      if (session is null)
      {
        signaler.Dispose();
      }
    }
  }

  public async Task<FileSystemOutcome<FileTransferSession>> StartFileDownload(
    ClaimsPrincipal user,
    Guid deviceId,
    string filePath,
    CancellationToken cancellationToken,
    Guid? expectedTenantId = null)
  {
    var guarded = await Guard(
      user,
      deviceId,
      DeviceResourcePolicies.FileSystemTransferDownload,
      expectedTenantId,
      cancellationToken);

    if (guarded is not { Succeeded: true, Value: { } device })
    {
      return new(guarded.Failure, guarded.Reason, null);
    }

    var streamId = Guid.NewGuid();
    var signaler = _hubStreamStore.GetOrCreate<byte[]>(streamId, HubStreamExpiration.FileTransfer);
    FileTransferSession? session = null;

    try
    {
      var downloadRequest = new FileDownloadHubDto(streamId, filePath);

      var result = await _agentHub.Clients
        .Client(device.ConnectionId)
        .UploadFileToViewer(downloadRequest);

      if (result is null)
      {
        _logger.LogWarning("No response received from agent for file download of {FilePath} on device {DeviceId}",
          filePath, deviceId);
        return new(FileSystemFailure.NoResponse, null, null);
      }

      if (!result.IsSuccess)
      {
        _logger.LogWarning("File download request failed for {FilePath} on device {DeviceId}: {Reason}",
          filePath, deviceId, result.Reason);
        return new(FileSystemFailure.RemoteFailure, result.Reason, null);
      }

      _logger.LogInformation("File download started for {FilePath} from device {DeviceId}",
        filePath, deviceId);

      session = new FileTransferSession(
        signaler,
        result.Value.FileDisplayName,
        result.Value.FileSize,
        cancellationToken);

      return new(FileSystemFailure.None, null, session);
    }
    catch (OperationCanceledException)
    {
      _logger.LogWarning("File download for {FilePath} from device {DeviceId} was canceled.",
        filePath, deviceId);
      return new(FileSystemFailure.Cancelled, null, null);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error starting file download of {FilePath} from device {DeviceId}",
        filePath, deviceId);
      return new(FileSystemFailure.Unexpected, ex.Message, null);
    }
    finally
    {
      if (session is null)
      {
        signaler.Dispose();
      }
    }
  }

  public async Task<FileSystemOutcome<FileTransferSession>> StartLogFileContents(
    ClaimsPrincipal user,
    Guid deviceId,
    string filePath,
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

    var streamId = Guid.NewGuid();
    var signaler = _hubStreamStore.GetOrCreate<byte[]>(streamId, HubStreamExpiration.FileTransfer);
    FileTransferSession? session = null;

    try
    {
      var streamRequest = new StreamFileContentsRequestHubDto(streamId, filePath);

      var result = await _agentHub.Clients
        .Client(device.ConnectionId)
        .StreamFileContents(streamRequest);

      if (result is null)
      {
        _logger.LogWarning("No response received from agent for log file contents of {FilePath} on device {DeviceId}",
          filePath, deviceId);
        return new(FileSystemFailure.NoResponse, null, null);
      }

      if (!result.IsSuccess)
      {
        _logger.LogWarning("Log file contents stream request failed for {FilePath} on device {DeviceId}: {Reason}",
          filePath, deviceId, result.Reason);
        return new(FileSystemFailure.RemoteFailure, result.Reason, null);
      }

      // The agent streams text with no length, so the response states none.
      session = new FileTransferSession(signaler, Path.GetFileName(filePath), null, cancellationToken);

      return new(FileSystemFailure.None, null, session);
    }
    catch (OperationCanceledException)
    {
      _logger.LogWarning("Log file contents stream for {FilePath} on device {DeviceId} was canceled.",
        filePath, deviceId);
      return new(FileSystemFailure.Cancelled, null, null);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error starting log file contents stream for {FilePath} on device {DeviceId}",
        filePath, deviceId);
      return new(FileSystemFailure.Unexpected, ex.Message, null);
    }
    finally
    {
      if (session is null)
      {
        signaler.Dispose();
      }
    }
  }

  public async Task<FileSystemOutcome> UploadFile(
    ClaimsPrincipal user,
    Guid deviceId,
    Stream fileStream,
    string fileName,
    long fileLength,
    string targetSaveDirectory,
    bool overwrite,
    CancellationToken cancellationToken,
    Guid? expectedTenantId = null)
  {
    var guarded = await Guard(
      user,
      deviceId,
      DeviceResourcePolicies.FileSystemTransferUpload,
      expectedTenantId,
      cancellationToken);

    if (guarded is not { Succeeded: true, Value: { } device })
    {
      return new(guarded.Failure, guarded.Reason);
    }

    var streamId = Guid.NewGuid();
    using var signaler = _hubStreamStore.GetOrCreate<byte[]>(streamId, HubStreamExpiration.FileTransfer);
    var uploadRequest = new FileUploadHubDto(
      streamId,
      targetSaveDirectory,
      fileName,
      fileLength,
      overwrite);

    try
    {
      // The bytes start flowing before the agent is asked to read them, so neither side has to buffer
      // the whole file.
      var writeToStreamTask = signaler.WriteFromStream(fileStream, cancellationToken);

      var result = await _agentHub.Clients
        .Client(device.ConnectionId)
        .DownloadFileFromViewer(uploadRequest);

      if (result is null)
      {
        _logger.LogWarning("No response received from agent for file upload of {FileName} to device {DeviceId}",
          fileName, deviceId);
        await AbandonUpload(writeToStreamTask, signaler);
        return new(FileSystemFailure.NoResponse, null);
      }

      if (!result.IsSuccess)
      {
        _logger.LogWarning("File upload request failed for {FileName} to device {DeviceId}: {Reason}",
          fileName, deviceId, result.Reason);
        await AbandonUpload(writeToStreamTask, signaler);
        return new(FileSystemFailure.RemoteFailure, result.Reason);
      }

      // Only an agent that accepted is draining the channel. Waiting for the copy before that answer
      // parks the request forever, because the writer stops once the bounded channel fills and
      // nothing is left to read it.
      await writeToStreamTask;

      _logger.LogInformation("File upload completed for {FileName} to device {DeviceId}",
        fileName, deviceId);

      return new(FileSystemFailure.None, null);
    }
    catch (OperationCanceledException)
    {
      _logger.LogWarning("File upload for {FileName} to device {DeviceId} timed out or was canceled.",
        fileName, deviceId);
      return new(FileSystemFailure.Cancelled, null);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error uploading file {FileName} to device {DeviceId}",
        fileName, deviceId);
      return new(FileSystemFailure.Unexpected, ex.Message);
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

      // The agent's reply is the answer itself rather than a hub result wrapping it. An agent that
      // never answered produces nothing, reported so the caller answers 502.
      if (result is null)
      {
        _logger.LogWarning("No response received from agent for path validation on device {DeviceId}", deviceId);
        return new(FileSystemFailure.NoResponse, null, null);
      }

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
  /// Ends an upload the agent will not read. Once the agent has answered no there is nothing left to
  /// drain the channel, so the copy has to be stopped and its fault observed rather than left running
  /// against a channel nobody reads.
  /// </summary>
  private static async Task AbandonUpload(Task copyTask, HubStreamSignaler<byte[]> signaler)
  {
    signaler.Dispose();

    try
    {
      await copyTask;
    }
    catch (Exception ex) when (ex is ChannelClosedException or OperationCanceledException)
    {
      // The expected end of a copy whose reader is gone.
    }
  }

  /// <summary>
  /// Loads the device, applies the operation's device resource policy, and requires the agent online.
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
  /// Loads the target device by id, with an explicit tenant predicate when one is supplied.
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
  /// Opens a stream session, asks the agent to fill it, and collects what arrives before it closes.
  /// </summary>
  private async Task<FileSystemOutcome<StreamedEntries>> StreamEntries(
    Device device,
    string operationName,
    string directoryPath,
    Func<Guid, Task<HubResult>> startStream,
    CancellationToken cancellationToken)
  {
    var streamId = Guid.NewGuid();
    using var signaler = _hubStreamStore.GetOrCreate<InternalDtos.FileSystemEntryDto[]>(streamId, HubStreamExpiration.Listing);

    var result = await startStream(streamId);

    if (result is null)
    {
      _logger.LogWarning("No response received from agent for {OperationName} stream on device {DeviceId} path {DirectoryPath}",
        operationName, device.Id, directoryPath);
      return new(FileSystemFailure.NoResponse, null, null);
    }

    if (!result.IsSuccess)
    {
      _logger.LogWarning("Failed to initiate {OperationName} stream for device {DeviceId} path {DirectoryPath}: {Reason}",
        operationName, device.Id, directoryPath, result.Reason);
      return new(FileSystemFailure.RemoteFailure, result.Reason, null);
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
  /// signal the agent left in the stream session's metadata.
  /// </summary>
  private sealed record StreamedEntries(List<InternalDtos.FileSystemEntryDto> Items, bool DirectoryExists);
}
