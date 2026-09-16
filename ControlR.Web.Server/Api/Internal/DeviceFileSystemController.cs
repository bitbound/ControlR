using ControlR.Libraries.Api.Contracts.Dtos.HubDtos;
using ControlR.Libraries.Api.Contracts.Hubs.Clients;
using ControlR.Libraries.Shared.Helpers;
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
  private const string DeviceOfflineMessage = "Device is not currently online.";

  [HttpPost("create-directory/{deviceId:guid}")]
  [ApiDeprecated("/api/v1/device-file-system/create-directory/{deviceId}?tenantId={tenantId}", Note = "Use POST /api/v1/device-file-system/create-directory/{deviceId} with a required tenantId. The V1 body carries no DeviceId, because the route already names the device. V1 answers an unknown device with 404, an offline device with 400, a canceled wait with 408, and 409 carrying the agent's own text when the device answers with a refusal, instead of answering 204 whatever the agent said.")]
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

    // This endpoint has answered 204 as soon as the request reached the agent, whether or not the
    // agent accepted it. The rejection the service reports is left unused here deliberately. Making
    // it mean something is a behavior change that does not belong in an extraction.
    return outcome.Failure switch
    {
      FileSystemFailure.DeviceNotFound => NotFound(),
      FileSystemFailure.Forbidden => Forbid(),
      FileSystemFailure.DeviceOffline => BadRequest(DeviceOfflineMessage),
      FileSystemFailure.Cancelled or FileSystemFailure.Unexpected => StatusCode(500, "An error occurred during directory creation."),
      _ => NoContent(),
    };
  }

  [HttpDelete("delete-path/{deviceId:guid}")]
  [ApiDeprecated("/api/v1/device-file-system/delete-path/{deviceId}?tenantId={tenantId}", Note = "Use DELETE /api/v1/device-file-system/delete-path/{deviceId} with a required tenantId. The V1 response is the named DevicePathDeletionResponseDto instead of an ad hoc body whose key order depended on an anonymous type, and its request carries no DeviceId or IsDirectory. V1 answers an unknown device with 404, an offline device with 400, a canceled wait with 408, and 409 carrying the agent's own text when the device answers with a refusal.")]
  public async Task<IActionResult> DeletePath(
    [FromRoute] Guid deviceId,
    [FromBody] InternalDtos.FileDeleteRequestDto request,
    [FromServices] IDeviceFileSystemService deviceFileSystem,
    CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(request.FilePath))
    {
      return BadRequest("File path is required.");
    }

    var outcome = await deviceFileSystem.DeletePath(User, deviceId, request, cancellationToken);

    // As with directory creation, the agent's verdict is discarded. What this endpoint reports is the
    // deletion it requested. The payload is an anonymous type, so its property order is the response
    // body's key order.
    return outcome.Failure switch
    {
      FileSystemFailure.DeviceNotFound => NotFound(),
      FileSystemFailure.Forbidden => Forbid(),
      FileSystemFailure.DeviceOffline => BadRequest(DeviceOfflineMessage),
      FileSystemFailure.Cancelled or FileSystemFailure.Unexpected => StatusCode(500, "An error occurred during file deletion."),
      _ => Ok(new { Message = "File deletion completed", request.FilePath }),
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
      return BadRequest("Device is not currently online.");
    }

    var streamId = Guid.NewGuid();
    using var signaler = hubStreamStore.GetOrCreate<byte[]>(streamId, TimeSpan.FromMinutes(30));

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
  [ApiDeprecated("/api/v1/device-file-system/contents?tenantId={tenantId}", Note = "Use POST /api/v1/device-file-system/contents with a required tenantId. The response is the same listing under the V1 type names. V1 answers an unknown device with 404, an offline device with 400, a canceled wait with 408, and 409 carrying the agent's own text when the device answers with a refusal, instead of a 400 carrying the reason as a bare string.")]
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
      FileSystemFailure.DeviceOffline => BadRequest(DeviceOfflineMessage),
      FileSystemFailure.HubRejected => BadRequest(outcome.Reason),
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
      return BadRequest("Device is not currently online.");
    }

    var streamId = Guid.NewGuid();
    using var signaler = hubStreamStore.GetOrCreate<byte[]>(streamId, TimeSpan.FromMinutes(30));

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
          title: "A failure occurred on the remote device.");
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
  [ApiDeprecated("/api/v1/device-file-system/logs/{deviceId}?tenantId={tenantId}", Note = "Use GET /api/v1/device-file-system/logs/{deviceId} with a required tenantId. The response is the same grouping under the V1 type names. V1 answers an unknown device with 404, an offline device with 400, a canceled wait with 408, and 409 carrying the agent's own text when the device answers with a refusal, instead of a 500.")]
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
      FileSystemFailure.DeviceOffline => BadRequest(DeviceOfflineMessage),
      FileSystemFailure.HubRejected => Problem(
        detail: outcome.Reason,
        statusCode: StatusCodes.Status500InternalServerError,
        title: "A failure occurred on the remote device."),
      FileSystemFailure.Cancelled or FileSystemFailure.Unexpected => Problem(
        detail: "An error occurred while retrieving log files.",
        statusCode: StatusCodes.Status500InternalServerError,
        title: "Error retrieving log files."),
      _ => Ok(outcome.Value),
    };
  }

  [HttpPost("path-segments")]
  [ApiDeprecated("/api/v1/device-file-system/path-segments?tenantId={tenantId}", Note = "Use POST /api/v1/device-file-system/path-segments with a required tenantId. The response is the same answer under the V1 type names. V1 answers an unknown device with 404 rather than this endpoint's 400, an offline device with 400, a canceled wait with 408, and 409 carrying the agent's own text when the device answers with a refusal, or 502 when no answer arrived, instead of a 500.")]
  public async Task<IActionResult> GetPathSegments(
    [FromBody] InternalDtos.GetPathSegmentsRequestDto request,
    [FromServices] IDeviceFileSystemService deviceFileSystem,
    CancellationToken cancellationToken)
  {
    var outcome = await deviceFileSystem.GetPathSegments(User, request, cancellationToken);

    // Of the eight endpoints, this is the only one that answers a missing device with 400, the only
    // one that leaves a rejected authorization unlogged, and the only one that distinguishes an agent
    // that never answered from an agent that answered with a rejection.
    return outcome.Failure switch
    {
      FileSystemFailure.DeviceNotFound => BadRequest("Device not found."),
      FileSystemFailure.Forbidden => Forbid(),
      FileSystemFailure.DeviceOffline => BadRequest(DeviceOfflineMessage),
      FileSystemFailure.HubRejected => StatusCode(500, "No response received from device agent."),
      FileSystemFailure.Cancelled or FileSystemFailure.Unexpected => StatusCode(500, "An error occurred while getting path segments."),
      _ => Ok(outcome.Value),
    };
  }

  [HttpPost("root-drives")]
  [ApiDeprecated("/api/v1/device-file-system/root-drives?tenantId={tenantId}", Note = "Use POST /api/v1/device-file-system/root-drives with a required tenantId. The response is the same listing under the V1 type names. V1 answers an unknown device with 404, an offline device with 400, a canceled wait with 408, and 409 carrying the agent's own text when the device answers with a refusal, instead of a 400 carrying the reason as a bare string.")]
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
      FileSystemFailure.DeviceOffline => BadRequest(DeviceOfflineMessage),
      FileSystemFailure.HubRejected => BadRequest(outcome.Reason),
      FileSystemFailure.Cancelled or FileSystemFailure.Unexpected => StatusCode(500, "An error occurred while retrieving root drives."),
      _ => Ok(outcome.Value),
    };
  }

  [HttpPost("subdirectories")]
  [ApiDeprecated("/api/v1/device-file-system/subdirectories?tenantId={tenantId}", Note = "Use POST /api/v1/device-file-system/subdirectories with a required tenantId. The response is the same listing under the V1 type names, and still carries no directory-exists signal. V1 answers an unknown device with 404, an offline device with 400, a canceled wait with 408, and 409 carrying the agent's own text when the device answers with a refusal, instead of a 400 carrying the reason as a bare string.")]
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
      FileSystemFailure.DeviceOffline => BadRequest(DeviceOfflineMessage),
      FileSystemFailure.HubRejected => BadRequest(outcome.Reason),
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
      return BadRequest("Device is not currently online.");
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
    using var signaler = hubStreamStore.GetOrCreate<byte[]>(streamId, TimeSpan.FromMinutes(30));
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
  [ApiDeprecated("/api/v1/device-file-system/validate-path/{deviceId}?tenantId={tenantId}", Note = "Use POST /api/v1/device-file-system/validate-path/{deviceId} with a required tenantId. The V1 body carries no DeviceId, because the route already names the device. The answer is the same, including an answer that the path is invalid. V1 answers an unknown device with 404, an offline device with 400, and a canceled wait with 408.")]
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

    // The agent's answer is returned whole, including an answer that the path is invalid. There is no
    // rejection for this endpoint to report, because the agent's reply is the answer itself rather
    // than a hub result wrapping it.
    return outcome.Failure switch
    {
      FileSystemFailure.DeviceNotFound => NotFound(),
      FileSystemFailure.Forbidden => Forbid(),
      FileSystemFailure.DeviceOffline => BadRequest(DeviceOfflineMessage),
      FileSystemFailure.Cancelled or FileSystemFailure.Unexpected => StatusCode(500, "An error occurred while validating the file path."),
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
      return BadRequest("Device is not currently online.");
    }

    var streamId = Guid.NewGuid();
    using var signaler = hubStreamStore.GetOrCreate<byte[]>(streamId, TimeSpan.FromMinutes(30));
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
}
