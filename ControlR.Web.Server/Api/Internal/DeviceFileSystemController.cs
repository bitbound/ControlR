using ControlR.Libraries.Api.Contracts.Dtos.HubDtos;
using ControlR.Libraries.Api.Contracts.Hubs.Clients;
using ControlR.Libraries.Shared.Helpers;
using ControlR.Web.Server.Constants;
using ControlR.Web.Server.Services.DeviceFileSystem;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Net.Http.Headers;

namespace ControlR.Web.Server.Api.Internal;

[Route(HttpConstants.Internal.DeviceFileSystemEndpoint)]
[ApiController]
[Authorize]
[EndpointGroupName(OpenApiConstants.InternalGroupName)]
public class DeviceFileSystemController : ControllerBase
{
  private const string DeviceOfflineMessage = "Device is currently offline.";
  private const string NoResponseMessage = "No response received from device agent.";

  [HttpPost("create-directory/{deviceId:guid}")]
  [ApiDeprecated("/api/v1/device-file-system/create-directory/{deviceId}?tenantId={tenantId}", Note = "Use POST /api/v1/device-file-system/create-directory/{deviceId} with a required tenantId. The V1 body carries no DeviceId, because the route already names the device. V1 answers every failure with a ProblemDetails body, where this endpoint answers some of them with bare strings.")]
  public async Task<IActionResult> CreateDirectory(
    [FromRoute] Guid deviceId,
    [FromBody] InternalDtos.CreateDirectoryRequestDto request,
    [FromServices] IDeviceFileSystemService deviceFileSystem,
    CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(request.ParentPath) || string.IsNullOrWhiteSpace(request.DirectoryName))
    {
      return BadRequest("Parent path and directory name are required.");
    }

    var outcome = await deviceFileSystem.CreateDirectory(User, deviceId, request, cancellationToken);

    return outcome.Failure switch
    {
      FileSystemFailure.DeviceNotFound => NotFound(),
      FileSystemFailure.Forbidden => Forbid(),
      FileSystemFailure.DeviceOffline => Conflict(DeviceOfflineMessage),
      FileSystemFailure.RemoteFailure => RemoteFailureProblem(outcome.Reason),
      FileSystemFailure.NoResponse => NoResponseProblem(),
      FileSystemFailure.Cancelled or FileSystemFailure.Unexpected => StatusCode(500, "An error occurred during directory creation."),
      _ => NoContent(),
    };
  }

  [HttpDelete("delete-path/{deviceId:guid}")]
  [ApiDeprecated("/api/v1/device-file-system/delete-path/{deviceId}?tenantId={tenantId}", Note = "Use DELETE /api/v1/device-file-system/delete-path/{deviceId} with a required tenantId. The V1 body carries no DeviceId, because the route already names the device. The V1 response is the named DevicePathDeletionResponseDto instead of an ad hoc body whose key order depended on an anonymous type. V1 answers every failure with a ProblemDetails body, where this endpoint answers some of them with bare strings.")]
  public async Task<IActionResult> DeletePath(
    [FromRoute] Guid deviceId,
    [FromBody] InternalDtos.DeletePathRequestDto request,
    [FromServices] IDeviceFileSystemService deviceFileSystem,
    CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(request.FilePath))
    {
      return BadRequest("A path is required.");
    }

    var outcome = await deviceFileSystem.DeletePath(User, deviceId, request, cancellationToken);

    // The payload is an anonymous type, so its property order is the response body's key order.
    return outcome.Failure switch
    {
      FileSystemFailure.DeviceNotFound => NotFound(),
      FileSystemFailure.Forbidden => Forbid(),
      FileSystemFailure.DeviceOffline => Conflict(DeviceOfflineMessage),
      FileSystemFailure.RemoteFailure => RemoteFailureProblem(outcome.Reason),
      FileSystemFailure.NoResponse => NoResponseProblem(),
      FileSystemFailure.Cancelled or FileSystemFailure.Unexpected => StatusCode(500, "An error occurred during path deletion."),
      _ => Ok(new { Message = "Path deletion completed", request.FilePath }),
    };
  }

  [HttpPost("download-archive/{deviceId:guid}")]
  [DisableRequestTimeout]
  public async Task<IActionResult> DownloadArchive(
    [FromRoute] Guid deviceId,
    [FromBody] InternalDtos.DownloadArchiveRequestDto request,
    [FromServices] AppDb appDb,
    [FromServices] IHubContext<AgentHub, IAgentHubClient> agentHub,
    [FromServices] IHubStreamStore hubStreamStore,
    [FromServices] IAuthorizationService authorizationService,
    [FromServices] IOptionsMonitor<AppOptions> appOptions,
    [FromServices] ILogger<DeviceFileSystemController> logger,
    CancellationToken cancellationToken)
  {
    return await ExecuteDownloadArchive(deviceId, request, appDb, agentHub, hubStreamStore, authorizationService, appOptions, logger, cancellationToken);
  }

  [HttpPost("download-archive/{deviceId:guid}/form")]
  [DisableRequestTimeout]
  [Consumes("application/x-www-form-urlencoded")]
  [ApiExplorerSettings(IgnoreApi = true)]
  public async Task<IActionResult> DownloadArchiveForm(
    [FromRoute] Guid deviceId,
    [FromServices] AppDb appDb,
    [FromServices] IHubContext<AgentHub, IAgentHubClient> agentHub,
    [FromServices] IHubStreamStore hubStreamStore,
    [FromServices] IAuthorizationService authorizationService,
    [FromServices] IOptionsMonitor<AppOptions> appOptions,
    [FromServices] ILogger<DeviceFileSystemController> logger,
    CancellationToken cancellationToken)
  {
    var form = await Request.ReadFormAsync(cancellationToken);
    var request = new InternalDtos.DownloadArchiveRequestDto(
      form["archiveFileName"].ToString(),
      form["targetPaths"]
        .Select(path => path ?? string.Empty)
        .Where(path => !string.IsNullOrWhiteSpace(path))
        .ToArray());

    return await ExecuteDownloadArchive(deviceId, request, appDb, agentHub, hubStreamStore, authorizationService, appOptions, logger, cancellationToken);
  }

  [HttpGet("download/{deviceId:guid}")]
  [DisableRequestTimeout]
  public async Task<IActionResult> DownloadFile(
    [FromRoute] Guid deviceId,
    [FromQuery] string filePath,
    [FromServices] AppDb appDb,
    [FromServices] IHubContext<AgentHub, IAgentHubClient> agentHub,
    [FromServices] IHubStreamStore hubStreamStore,
    [FromServices] IAuthorizationService authorizationService,
    [FromServices] IOptionsMonitor<AppOptions> appOptions,
    [FromServices] ILogger<DeviceFileSystemController> logger,
    CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(filePath))
    {
      return BadRequest("File path is required.");
    }

    var device = await appDb.Devices
      .AsNoTracking()
      .FirstOrDefaultAsync(x => x.Id == deviceId, cancellationToken);

    if (device is null)
    {
      logger.LogWarning("Device {DeviceId} not found.", deviceId);
      return NotFound();
    }

    var authResult = await authorizationService.AuthorizeAsync(
      User,
      device,
      DeviceResourcePolicies.FileSystemTransferDownload);

    if (!authResult.Succeeded)
    {
      logger.LogCritical("Authorization failed for user {UserName} on device {DeviceId}.",
        User.Identity?.Name, deviceId);
      return Forbid();
    }

    if (!device.IsOnline)
    {
      logger.LogWarning("Device {DeviceId} is not online.", deviceId);
      return Conflict(DeviceOfflineMessage);
    }

    var streamId = Guid.NewGuid();
    using var signaler = hubStreamStore.GetOrCreate<byte[]>(streamId, HubStreamExpiration.FileTransfer);

    var downloadRequest = new FileDownloadHubDto(streamId, filePath);

    try
    {
      var requestResult = await agentHub.Clients
        .Client(device.ConnectionId)
        .UploadFileToViewer(downloadRequest);

      if (!requestResult.IsSuccess)
      {
        logger.LogWarning("File download request failed for {FilePath} on device {DeviceId}.",
          filePath, deviceId);
        return StatusCode(StatusCodes.Status500InternalServerError);
      }

      var fileSize = requestResult.Value.FileSize;
      var maxFileSize = appOptions.CurrentValue.MaxFileTransferSize;
      if (maxFileSize > 0 && fileSize > maxFileSize)
      {
        return StatusCode(StatusCodes.Status413RequestEntityTooLarge);
      }

      Response.ContentType = "application/octet-stream";
      var contentDisposition = new ContentDispositionHeaderValue("attachment");
      contentDisposition.SetHttpFileName(requestResult.Value.FileDisplayName);
      Response.Headers[HeaderNames.ContentDisposition] = contentDisposition.ToString();
      Response.Headers.ContentLength = fileSize;

      await foreach (var chunk in signaler.Reader.ReadAllAsync(cancellationToken))
      {
        await Response.Body.WriteAsync(chunk, cancellationToken);
      }

      logger.LogInformation("File download completed for {FilePath} from device {DeviceId}",
        filePath, deviceId);

      return new EmptyResult();
    }
    catch (OperationCanceledException)
    {
      logger.LogWarning("File download for {FilePath} from device {DeviceId} timed out or was canceled.",
        filePath, deviceId);
      return StatusCode(StatusCodes.Status408RequestTimeout);
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error downloading file {FilePath} from device {DeviceId}",
        filePath, deviceId);
      return StatusCode(500, "An error occurred during file download.");
    }
  }

  [HttpPost("contents")]
  [ApiDeprecated("/api/v1/device-file-system/contents?tenantId={tenantId}", Note = "Use POST /api/v1/device-file-system/contents with a required tenantId. The response is the same listing under the V1 type names. V1 answers every failure with a ProblemDetails body, where this endpoint answers some of them with bare strings.")]
  public async Task<IActionResult> GetDirectoryContents(
    [FromBody] InternalDtos.GetDirectoryContentsRequestDto request,
    [FromServices] IDeviceFileSystemService deviceFileSystem,
    CancellationToken cancellationToken)
  {
    var outcome = await deviceFileSystem.GetDirectoryContents(User, request, cancellationToken);

    return outcome.Failure switch
    {
      FileSystemFailure.DeviceNotFound => NotFound(),
      FileSystemFailure.Forbidden => Forbid(),
      FileSystemFailure.DeviceOffline => Conflict(DeviceOfflineMessage),
      FileSystemFailure.RemoteFailure => RemoteFailureProblem(outcome.Reason),
      FileSystemFailure.NoResponse => NoResponseProblem(),
      FileSystemFailure.Cancelled => StatusCode(StatusCodes.Status408RequestTimeout),
      FileSystemFailure.Unexpected => StatusCode(500, "An error occurred while retrieving directory contents."),
      _ => Ok(outcome.Value),
    };
  }

  [HttpGet("logs/{deviceId:guid}/contents")]
  [DisableRequestTimeout]
  public async Task<IActionResult> GetLogFileContents(
    [FromRoute] Guid deviceId,
    [FromQuery] string filePath,
    [FromServices] AppDb appDb,
    [FromServices] IHubContext<AgentHub, IAgentHubClient> agentHub,
    [FromServices] IHubStreamStore hubStreamStore,
    [FromServices] IAuthorizationService authorizationService,
    [FromServices] ILogger<DeviceFileSystemController> logger,
    CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(filePath))
    {
      return BadRequest("File path is required.");
    }

    var device = await appDb.Devices
      .AsNoTracking()
      .FirstOrDefaultAsync(x => x.Id == deviceId, cancellationToken);

    if (device is null)
    {
      logger.LogWarning("Device {DeviceId} not found.", deviceId);
      return NotFound();
    }

    var authResult = await authorizationService.AuthorizeAsync(
      User,
      device,
      DeviceResourcePolicies.LogsRead);

    if (!authResult.Succeeded)
    {
      logger.LogCritical("Authorization failed for user {UserName} on device {DeviceId}.",
        User.Identity?.Name, deviceId);
      return Forbid();
    }

    if (!device.IsOnline)
    {
      logger.LogWarning("Device {DeviceId} is not online.", deviceId);
      return Conflict(DeviceOfflineMessage);
    }

    var streamId = Guid.NewGuid();
    using var signaler = hubStreamStore.GetOrCreate<byte[]>(streamId, HubStreamExpiration.FileTransfer);

    var streamRequest = new StreamFileContentsRequestHubDto(streamId, filePath);

    try
    {
      var streamResult = await agentHub
        .Clients
        .Client(device.ConnectionId)
        .StreamFileContents(streamRequest);

      if (!streamResult.IsSuccess)
      {
        logger.LogWarning("Log file contents stream request failed for {FilePath} on device {DeviceId}.",
          filePath, deviceId);
        return Problem(
          detail: streamResult.Reason,
          statusCode: StatusCodes.Status500InternalServerError,
          title: V1ProblemTitles.InternalServerError);
      }

      var fileName = Path.GetFileName(filePath);

      var contentDisposition = new ContentDispositionHeaderValue("inline")
      {
        FileName = fileName
      };

      Response.Headers.ContentDisposition = contentDisposition.ToString();
      Response.ContentType = "text/plain";

      await foreach (var chunk in signaler.Reader.ReadAllAsync(cancellationToken))
      {
        if (chunk.Length > 0)
        {
          await Response.Body.WriteAsync(chunk, cancellationToken);
        }
      }

      return new EmptyResult();
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error streaming log file {FilePath} from device {DeviceId}", filePath, deviceId);
      return StatusCode(500, "An error occurred while streaming the log file.");
    }
  }

  [HttpGet("logs/{deviceId:guid}")]
  [ApiDeprecated("/api/v1/device-file-system/logs/{deviceId}?tenantId={tenantId}", Note = "Use GET /api/v1/device-file-system/logs/{deviceId} with a required tenantId. The response is the same grouping under the V1 type names. V1 answers every failure with a ProblemDetails body, where this endpoint answers some of them with bare strings.")]
  public async Task<IActionResult> GetLogFiles(
    [FromRoute] Guid deviceId,
    [FromServices] IDeviceFileSystemService deviceFileSystem,
    CancellationToken cancellationToken)
  {
    var outcome = await deviceFileSystem.GetLogFiles(User, deviceId, cancellationToken);

    return outcome.Failure switch
    {
      FileSystemFailure.DeviceNotFound => NotFound(),
      FileSystemFailure.Forbidden => Forbid(),
      FileSystemFailure.DeviceOffline => Conflict(DeviceOfflineMessage),
      FileSystemFailure.RemoteFailure => RemoteFailureProblem(outcome.Reason),
      FileSystemFailure.NoResponse => NoResponseProblem(),
      FileSystemFailure.Cancelled or FileSystemFailure.Unexpected => Problem(
        detail: "An error occurred while retrieving log files.",
        statusCode: StatusCodes.Status500InternalServerError,
        title: V1ProblemTitles.InternalServerError),
      _ => Ok(outcome.Value),
    };
  }

  [HttpPost("path-segments")]
  [ApiDeprecated("/api/v1/device-file-system/path-segments?tenantId={tenantId}", Note = "Use POST /api/v1/device-file-system/path-segments with a required tenantId. The response is the same answer under the V1 type names. V1 answers an unknown device with 404 rather than this endpoint's 400, and answers every failure with a ProblemDetails body, where this endpoint answers some of them with bare strings.")]
  public async Task<IActionResult> GetPathSegments(
    [FromBody] InternalDtos.GetPathSegmentsRequestDto request,
    [FromServices] IDeviceFileSystemService deviceFileSystem,
    CancellationToken cancellationToken)
  {
    var outcome = await deviceFileSystem.GetPathSegments(User, request, cancellationToken);

    // Of the eight endpoints, this is the only one that answers a missing device with 400 and the only
    // one that leaves a rejected authorization unlogged. The agent's reply is the answer itself, so
    // there is no remote failure to report here beyond the agent never answering.
    return outcome.Failure switch
    {
      FileSystemFailure.DeviceNotFound => BadRequest("Device not found."),
      FileSystemFailure.Forbidden => Forbid(),
      FileSystemFailure.DeviceOffline => Conflict(DeviceOfflineMessage),
      FileSystemFailure.NoResponse => NoResponseProblem(),
      FileSystemFailure.Cancelled or FileSystemFailure.Unexpected => StatusCode(500, "An error occurred while getting path segments."),
      _ => Ok(outcome.Value),
    };
  }

  [HttpPost("root-drives")]
  [ApiDeprecated("/api/v1/device-file-system/root-drives?tenantId={tenantId}", Note = "Use POST /api/v1/device-file-system/root-drives with a required tenantId. The response is the same listing under the V1 type names. V1 answers every failure with a ProblemDetails body, where this endpoint answers some of them with bare strings.")]
  public async Task<IActionResult> GetRootDrives(
    [FromBody] InternalDtos.GetRootDrivesRequestDto request,
    [FromServices] IDeviceFileSystemService deviceFileSystem,
    CancellationToken cancellationToken)
  {
    var outcome = await deviceFileSystem.GetRootDrives(User, request, cancellationToken);

    return outcome.Failure switch
    {
      FileSystemFailure.DeviceNotFound => NotFound(),
      FileSystemFailure.Forbidden => Forbid(),
      FileSystemFailure.DeviceOffline => Conflict(DeviceOfflineMessage),
      FileSystemFailure.RemoteFailure => RemoteFailureProblem(outcome.Reason),
      FileSystemFailure.NoResponse => NoResponseProblem(),
      FileSystemFailure.Cancelled or FileSystemFailure.Unexpected => StatusCode(500, "An error occurred while retrieving root drives."),
      _ => Ok(outcome.Value),
    };
  }

  [HttpPost("subdirectories")]
  [ApiDeprecated("/api/v1/device-file-system/subdirectories?tenantId={tenantId}", Note = "Use POST /api/v1/device-file-system/subdirectories with a required tenantId. The response is the same listing under the V1 type names, and still carries no directory-exists signal. V1 answers every failure with a ProblemDetails body, where this endpoint answers some of them with bare strings.")]
  public async Task<IActionResult> GetSubdirectories(
    [FromBody] InternalDtos.GetSubdirectoriesRequestDto request,
    [FromServices] IDeviceFileSystemService deviceFileSystem,
    CancellationToken cancellationToken)
  {
    var outcome = await deviceFileSystem.GetSubdirectories(User, request, cancellationToken);

    // The directory-exists signal the agent leaves in the stream metadata has no place in this
    // endpoint's response, unlike its directory contents sibling.
    return outcome.Failure switch
    {
      FileSystemFailure.DeviceNotFound => NotFound(),
      FileSystemFailure.Forbidden => Forbid(),
      FileSystemFailure.DeviceOffline => Conflict(DeviceOfflineMessage),
      FileSystemFailure.RemoteFailure => RemoteFailureProblem(outcome.Reason),
      FileSystemFailure.NoResponse => NoResponseProblem(),
      FileSystemFailure.Cancelled => StatusCode(StatusCodes.Status408RequestTimeout),
      FileSystemFailure.Unexpected => StatusCode(500, "An error occurred while retrieving subdirectories."),
      _ => Ok(outcome.Value),
    };
  }

  // Note: [FromForm] parameters are intentionally omitted, so large files aren't
  // buffered into memory by model binding before auth and size checks are run. 
  // The form  fields are added to OpenAPI metadata in FileUploadTransformer, and 
  // file size limits are checked below.
  [HttpPost("upload/{deviceId:guid}")]
  [DisableRequestSizeLimit]
  [DisableRequestTimeout]
  public async Task<IActionResult> UploadFile(
    [FromRoute] Guid deviceId,
    [FromServices] AppDb appDb,
    [FromServices] IHubContext<AgentHub, IAgentHubClient> agentHub,
    [FromServices] IHubStreamStore hubStreamStore,
    [FromServices] IAuthorizationService authorizationService,
    [FromServices] IOptionsMonitor<AppOptions> appOptions,
    [FromServices] ILogger<DeviceFileSystemController> logger,
    CancellationToken cancellationToken)
  {
    var device = await appDb.Devices
      .AsNoTracking()
      .FirstOrDefaultAsync(x => x.Id == deviceId, cancellationToken);

    if (device is null)
    {
      logger.LogWarning("Device {DeviceId} not found.", deviceId);
      return NotFound();
    }

    var authResult = await authorizationService.AuthorizeAsync(
      User,
      device,
      DeviceResourcePolicies.FileSystemTransferUpload);

    if (!authResult.Succeeded)
    {
      logger.LogCritical(
        "Authorization failed for user {UserName} on device {DeviceId}. Remote IP: {RemoteIpAddress}",
        User.Identity?.Name,
        deviceId,
        HttpContext.Connection.RemoteIpAddress);

      return Forbid();
    }

    if (!device.IsOnline)
    {
      logger.LogWarning("Device {DeviceId} is not online.", deviceId);
      return Conflict(DeviceOfflineMessage);
    }

    if (!Request.HasFormContentType)
    {
      return BadRequest("Expected multipart/form-data content type.");
    }

    var maxFileSize = appOptions.CurrentValue.MaxFileTransferSize;
    if (maxFileSize > 0 && Request.ContentLength > maxFileSize)
    {
      return StatusCode(StatusCodes.Status413RequestEntityTooLarge);
    }

    var form = await Request.ReadFormAsync(cancellationToken);
    var targetSaveDirectory = form["targetSaveDirectory"].ToString();
    var overwrite = bool.TryParse(form["overwrite"], out var overwriteValue) && overwriteValue;
    var file = form.Files.GetFile("file");

    if (file is null || file.Length == 0)
    {
      return BadRequest("File is required.");
    }

    if (string.IsNullOrWhiteSpace(targetSaveDirectory))
    {
      return BadRequest("Target save directory is required.");
    }

    var streamId = Guid.NewGuid();
    using var signaler = hubStreamStore.GetOrCreate<byte[]>(streamId, HubStreamExpiration.FileTransfer);
    var uploadRequest = new FileUploadHubDto(streamId, targetSaveDirectory, file.FileName, file.Length, overwrite);

    try
    {
      await using var stream = file.OpenReadStream();
      var writeToStreamTask = signaler.WriteFromStream(stream, cancellationToken);

      var requestResult = await agentHub.Clients
        .Client(device.ConnectionId)
        .DownloadFileFromViewer(uploadRequest);

      await writeToStreamTask.WaitAsync(cancellationToken);

      if (!requestResult.IsSuccess)
      {
        logger.LogWarning("File upload request failed for {FileName} to device {DeviceId}.",
          file.FileName, deviceId);
        return StatusCode(StatusCodes.Status500InternalServerError, requestResult.Reason);
      }

      logger.LogInformation("File upload completed for {FileName} to device {DeviceId}",
        file.FileName, deviceId);

      return Ok(new { Message = "File uploaded successfully", FileName = file.FileName });
    }
    catch (OperationCanceledException)
    {
      logger.LogWarning("File upload for {FileName} to device {DeviceId} timed out or was canceled.",
        file.FileName, deviceId);
      return StatusCode(StatusCodes.Status408RequestTimeout);
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error uploading file {FileName} to device {DeviceId}",
        file.FileName, deviceId);
      return StatusCode(500, "An error occurred during file upload.");
    }
  }

  [HttpPost("validate-path/{deviceId:guid}")]
  [ApiDeprecated("/api/v1/device-file-system/validate-path/{deviceId}?tenantId={tenantId}", Note = "Use POST /api/v1/device-file-system/validate-path/{deviceId} with a required tenantId. The V1 body carries no DeviceId, because the route already names the device. The answer is the same, including an answer that the path is invalid. V1 answers every failure with a ProblemDetails body, where this endpoint answers some of them with bare strings.")]
  public async Task<IActionResult> ValidateFilePath(
    [FromRoute] Guid deviceId,
    [FromBody] InternalDtos.ValidateFilePathRequestDto request,
    [FromServices] IDeviceFileSystemService deviceFileSystem,
    CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(request.DirectoryPath) || string.IsNullOrWhiteSpace(request.FileName))
    {
      return BadRequest("Directory path and file name are required.");
    }

    var outcome = await deviceFileSystem.ValidateFilePath(User, deviceId, request, cancellationToken);

    // The agent's answer is returned whole, including an answer that the path is invalid. The only
    // agent-side condition to report is that it never answered at all.
    return outcome.Failure switch
    {
      FileSystemFailure.DeviceNotFound => NotFound(),
      FileSystemFailure.Forbidden => Forbid(),
      FileSystemFailure.DeviceOffline => Conflict(DeviceOfflineMessage),
      FileSystemFailure.NoResponse => NoResponseProblem(),
      FileSystemFailure.Cancelled => Problem(
        detail: "The wait for the remote device was canceled.",
        statusCode: StatusCodes.Status408RequestTimeout,
        title: V1ProblemTitles.RequestTimedOut),
      FileSystemFailure.Unexpected =>
        StatusCode(500, "An error occurred while validating the file path."),
      _ => Ok(outcome.Value),
    };
  }

  private async Task<IActionResult> ExecuteDownloadArchive(
    Guid deviceId,
    InternalDtos.DownloadArchiveRequestDto request,
    AppDb appDb,
    IHubContext<AgentHub, IAgentHubClient> agentHub,
    IHubStreamStore hubStreamStore,
    IAuthorizationService authorizationService,
    IOptionsMonitor<AppOptions> appOptions,
    ILogger<DeviceFileSystemController> logger,
    CancellationToken cancellationToken)
  {
    if (request.TargetPaths is null || request.TargetPaths.Count == 0)
    {
      return BadRequest("At least one target path is required.");
    }

    var archiveFileName = ArchiveFileNameHelper.NormalizeArchiveFileName(request.ArchiveFileName);
    if (archiveFileName is null)
    {
      return BadRequest("Archive file name is invalid.");
    }

    var device = await appDb.Devices
      .AsNoTracking()
      .FirstOrDefaultAsync(x => x.Id == deviceId, cancellationToken);

    if (device is null)
    {
      logger.LogWarning("Device {DeviceId} not found.", deviceId);
      return NotFound();
    }

    var authResult = await authorizationService.AuthorizeAsync(
      User,
      device,
      DeviceResourcePolicies.FileSystemTransferDownload);

    if (!authResult.Succeeded)
    {
      logger.LogCritical("Authorization failed for user {UserName} on device {DeviceId}.",
        User.Identity?.Name, deviceId);
      return Forbid();
    }

    if (!device.IsOnline)
    {
      logger.LogWarning("Device {DeviceId} is not online.", deviceId);
      return Conflict(DeviceOfflineMessage);
    }

    var streamId = Guid.NewGuid();
    using var signaler = hubStreamStore.GetOrCreate<byte[]>(streamId, HubStreamExpiration.FileTransfer);
    var downloadRequest = new FileArchiveDownloadHubDto(streamId, archiveFileName, request.TargetPaths.ToArray());

    try
    {
      var requestResult = await agentHub.Clients
        .Client(device.ConnectionId)
        .UploadArchiveToViewer(downloadRequest);

      if (!requestResult.IsSuccess)
      {
        logger.LogWarning("Archive download request failed for device {DeviceId}: {Reason}",
          deviceId,
          requestResult.Reason);
        return StatusCode(StatusCodes.Status500InternalServerError, requestResult.Reason);
      }

      var fileSize = requestResult.Value.FileSize;
      var maxFileSize = appOptions.CurrentValue.MaxFileTransferSize;
      if (maxFileSize > 0 && fileSize > maxFileSize)
      {
        return StatusCode(StatusCodes.Status413RequestEntityTooLarge);
      }

      Response.ContentType = "application/octet-stream";
      var contentDisposition = new ContentDispositionHeaderValue("attachment");
      contentDisposition.SetHttpFileName(requestResult.Value.FileDisplayName);
      Response.Headers[HeaderNames.ContentDisposition] = contentDisposition.ToString();
      Response.Headers.ContentLength = fileSize;

      await foreach (var chunk in signaler.Reader.ReadAllAsync(cancellationToken))
      {
        await Response.Body.WriteAsync(chunk, cancellationToken);
      }

      logger.LogInformation("Archive download completed for device {DeviceId} with {ItemCount} item(s)",
        deviceId,
        request.TargetPaths.Count);

      return new EmptyResult();
    }
    catch (OperationCanceledException)
    {
      logger.LogWarning("Archive download for device {DeviceId} timed out or was canceled.", deviceId);
      return StatusCode(StatusCodes.Status408RequestTimeout);
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error downloading archive from device {DeviceId}", deviceId);
      return StatusCode(500, "An error occurred during archive download.");
    }
  }

  private ObjectResult NoResponseProblem()
  {
    return Problem(
      detail: NoResponseMessage,
      statusCode: StatusCodes.Status502BadGateway,
      title: V1ProblemTitles.BadGateway);
  }

  private ObjectResult RemoteFailureProblem(string? reason)
  {
    return Problem(
      detail: reason,
      statusCode: StatusCodes.Status409Conflict,
      title: V1ProblemTitles.Conflict);
  }
}
