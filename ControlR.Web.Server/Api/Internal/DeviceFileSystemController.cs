using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos.HubDtos;
using ControlR.Libraries.Api.Contracts.Hubs.Clients;
using ControlR.Libraries.Shared.Helpers;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Net.Http.Headers;

namespace ControlR.Web.Server.Api.Internal;

[Route(HttpConstants.Internal.DeviceFileSystemEndpoint)]
[Route(HttpConstants.Legacy.DeviceFileSystemEndpoint)]
[ApiController]
[Authorize]
[EndpointGroupName(OpenApiConstants.InternalGroupName)]
public class DeviceFileSystemController : ControllerBase
{
  [HttpPost("create-directory/{deviceId:guid}")]
  public async Task<IActionResult> CreateDirectory(
    [FromRoute] Guid deviceId,
    [FromBody] InternalDtos.CreateDirectoryRequestDto request,
    [FromServices] AppDb appDb,
    [FromServices] IHubContext<AgentHub, IAgentHubClient> agentHub,
    [FromServices] IAuthorizationService authorizationService,
    [FromServices] ILogger<DeviceFileSystemController> logger,
    CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(request.ParentPath) || string.IsNullOrWhiteSpace(request.DirectoryName))
    {
      return BadRequest("Parent path and directory name are required.");
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
      DeviceAccessByDeviceResourcePolicy.PolicyName);

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

    var createDirectoryRequest = new CreateDirectoryHubDto(request.ParentPath, request.DirectoryName);

    try
    {
      await agentHub.Clients
        .Client(device.ConnectionId)
        .CreateDirectory(createDirectoryRequest);

      logger.LogInformation("Directory creation requested for {DirectoryName} in {ParentPath} on device {DeviceId}",
        request.DirectoryName, request.ParentPath, deviceId);

      return NoContent();
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error creating directory {DirectoryName} in {ParentPath} on device {DeviceId}",
        request.DirectoryName, request.ParentPath, deviceId);
      return StatusCode(500, "An error occurred during directory creation.");
    }
  }

  [HttpDelete("delete-path/{deviceId:guid}")]
  public async Task<IActionResult> DeletePath(
    [FromRoute] Guid deviceId,
    [FromBody] InternalDtos.FileDeleteRequestDto request,
    [FromServices] AppDb appDb,
    [FromServices] IHubContext<AgentHub, IAgentHubClient> agentHub,
    [FromServices] IAuthorizationService authorizationService,
    [FromServices] ILogger<DeviceFileSystemController> logger,
    CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(request.FilePath))
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
      DeviceAccessByDeviceResourcePolicy.PolicyName);

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

    var deleteRequest = new FileDeleteHubDto(request.FilePath);

    try
    {
      await agentHub.Clients
        .Client(device.ConnectionId)
        .DeleteFile(deleteRequest);

      logger.LogInformation("File deletion requested for {FilePath} on device {DeviceId}",
        request.FilePath, deviceId);

      return Ok(new { Message = "File deletion completed", request.FilePath });
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error deleting file {FilePath} on device {DeviceId}",
        request.FilePath, deviceId);
      return StatusCode(500, "An error occurred during file deletion.");
    }
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
      DeviceAccessByDeviceResourcePolicy.PolicyName);

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
  public async Task<IActionResult> GetDirectoryContents(
    [FromBody] InternalDtos.GetDirectoryContentsRequestDto request,
    [FromServices] AppDb appDb,
    [FromServices] IHubContext<AgentHub, IAgentHubClient> agentHub,
    [FromServices] IHubStreamStore hubStreamStore,
    [FromServices] IAuthorizationService authorizationService,
    [FromServices] ILogger<DeviceFileSystemController> logger,
    CancellationToken cancellationToken)
  {
    var device = await appDb.Devices
      .AsNoTracking()
      .FirstOrDefaultAsync(x => x.Id == request.DeviceId, cancellationToken);

    if (device is null)
    {
      logger.LogWarning("Device {DeviceId} not found.", request.DeviceId);
      return NotFound();
    }

    var authResult = await authorizationService.AuthorizeAsync(
      User,
      device,
      DeviceAccessByDeviceResourcePolicy.PolicyName);

    if (!authResult.Succeeded)
    {
      logger.LogCritical("Authorization failed for user {UserName} on device {DeviceId}.",
        User.Identity?.Name, request.DeviceId);
      return Forbid();
    }

    if (!device.IsOnline)
    {
      logger.LogWarning("Device {DeviceId} is not online.", request.DeviceId);
      return BadRequest("Device is not currently online.");
    }

    var streamId = Guid.NewGuid();
    using var signaler = hubStreamStore.GetOrCreate<InternalDtos.FileSystemEntryDto[]>(streamId);
    try
    {
      var streamRequest = new DirectoryContentsStreamRequestHubDto(streamId, request.DeviceId, request.DirectoryPath);
      var result = await agentHub.Clients
        .Client(device.ConnectionId)
        .StreamDirectoryContents(streamRequest);

      if (!result.IsSuccess)
      {
        logger.LogWarning("Failed to initiate directory contents stream for device {DeviceId} path {DirectoryPath}: {Reason}",
          request.DeviceId, request.DirectoryPath, result.Reason);
        return BadRequest(result.Reason);
      }

      var entries = new List<InternalDtos.FileSystemEntryDto>();
      await foreach (var chunk in signaler.Reader.ReadAllAsync(cancellationToken))
      {
        entries.AddRange(chunk);
      }

      var directoryExists = signaler.Metadata is bool b && b;
      return Ok(new InternalDtos.GetDirectoryContentsResponseDto([.. entries], directoryExists));
    }
    catch (OperationCanceledException)
    {
      logger.LogWarning("Directory contents stream canceled/timed out for device {DeviceId} path {DirectoryPath}", request.DeviceId, request.DirectoryPath);
      return StatusCode(StatusCodes.Status408RequestTimeout);
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error while streaming directory contents for device {DeviceId} path {DirectoryPath}", request.DeviceId, request.DirectoryPath);
      return StatusCode(500, "An error occurred while retrieving directory contents.");
    }
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
      DeviceAccessByDeviceResourcePolicy.PolicyName);

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
  public async Task<IActionResult> GetLogFiles(
    [FromRoute] Guid deviceId,
    [FromServices] AppDb appDb,
    [FromServices] IHubContext<AgentHub, IAgentHubClient> agentHub,
    [FromServices] IAuthorizationService authorizationService,
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
      DeviceAccessByDeviceResourcePolicy.PolicyName);

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

    try
    {
      var result = await agentHub
        .Clients
        .Client(device.ConnectionId)
        .GetLogFiles();

      if (!result.IsSuccess)
      {
        logger.LogError("Get log files request failed for device {DeviceId}: {Reason}",
          deviceId, result.Reason);
        return Problem(
          detail: result.Reason,
          statusCode: StatusCodes.Status500InternalServerError,
          title: "A failure occurred on the remote device.");
      }

      return Ok(result.Value);
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error getting log files from device {DeviceId}", deviceId);
      return Problem(
        detail: "An error occurred while retrieving log files.",
        statusCode: StatusCodes.Status500InternalServerError,
        title: "Error retrieving log files.");
    }
  }

  [HttpPost("path-segments")]
  public async Task<IActionResult> GetPathSegments(
    [FromBody] InternalDtos.GetPathSegmentsRequestDto request,
    [FromServices] AppDb appDb,
    [FromServices] IHubContext<AgentHub, IAgentHubClient> agentHub,
    [FromServices] IAuthorizationService authorizationService,
    [FromServices] ILogger<DeviceFileSystemController> logger,
    CancellationToken cancellationToken)
  {
    try
    {
      var device = await appDb.Devices
        .AsNoTracking()
        .FirstOrDefaultAsync(x => x.Id == request.DeviceId, cancellationToken);

      if (device is null)
      {
        logger.LogWarning("Device not found for path segments request: {DeviceId}", request.DeviceId);
        return BadRequest("Device not found.");
      }

      var authResult = await authorizationService.AuthorizeAsync(
        User,
        device,
        DeviceAccessByDeviceResourcePolicy.PolicyName);
      if (!authResult.Succeeded)
      {
        return Forbid();
      }

      if (!device.IsOnline)
      {
        logger.LogWarning("Device {DeviceId} is not online.", request.DeviceId);
        return BadRequest("Device is not currently online.");
      }

      logger.LogInformation("Getting path segments for device {DeviceId} path {TargetPath}", request.DeviceId, request.TargetPath);

      var hubDto = new GetPathSegmentsHubDto { TargetPath = request.TargetPath };
      var result = await agentHub.Clients
        .Client(device.ConnectionId)
        .GetPathSegments(hubDto);

      if (result is null)
      {
        logger.LogWarning("No response received from agent for path segments request on device {DeviceId} path {TargetPath}", request.DeviceId, request.TargetPath);
        return StatusCode(500, "No response received from device agent.");
      }

      return Ok(result);
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error while getting path segments for device {DeviceId} path {TargetPath}", request.DeviceId, request.TargetPath);
      return StatusCode(500, "An error occurred while getting path segments.");
    }
  }

  [HttpPost("root-drives")]
  public async Task<IActionResult> GetRootDrives(
    [FromBody] InternalDtos.GetRootDrivesRequestDto request,
    [FromServices] AppDb appDb,
    [FromServices] IHubContext<AgentHub, IAgentHubClient> agentHub,
    [FromServices] IAuthorizationService authorizationService,
    [FromServices] ILogger<DeviceFileSystemController> logger,
    CancellationToken cancellationToken)
  {
    var device = await appDb.Devices
      .AsNoTracking()
      .FirstOrDefaultAsync(x => x.Id == request.DeviceId, cancellationToken);

    if (device is null)
    {
      logger.LogWarning("Device {DeviceId} not found.", request.DeviceId);
      return NotFound();
    }

    var authResult = await authorizationService.AuthorizeAsync(
      User,
      device,
      DeviceAccessByDeviceResourcePolicy.PolicyName);

    if (!authResult.Succeeded)
    {
      logger.LogCritical("Authorization failed for user {UserName} on device {DeviceId}.",
        User.Identity?.Name, request.DeviceId);
      return Forbid();
    }

    if (!device.IsOnline)
    {
      logger.LogWarning("Device {DeviceId} is not online.", request.DeviceId);
      return BadRequest("Device is not currently online.");
    }

    try
    {
      var result = await agentHub.Clients.Client(device.ConnectionId)
        .GetRootDrives(request);

      if (result.IsSuccess)
      {
        return Ok(result.Value);
      }

      logger.LogWarning("Failed to get root drives for device {DeviceId}: {Reason}",
        request.DeviceId, result.Reason);
      return BadRequest(result.Reason);
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error while getting root drives for device {DeviceId}", request.DeviceId);
      return StatusCode(500, "An error occurred while retrieving root drives.");
    }
  }

  [HttpPost("subdirectories")]
  public async Task<IActionResult> GetSubdirectories(
    [FromBody] InternalDtos.GetSubdirectoriesRequestDto request,
    [FromServices] AppDb appDb,
    [FromServices] IHubContext<AgentHub, IAgentHubClient> agentHub,
  [FromServices] IHubStreamStore hubStreamStore,
    [FromServices] IAuthorizationService authorizationService,
    [FromServices] ILogger<DeviceFileSystemController> logger,
    CancellationToken cancellationToken)
  {
    var device = await appDb.Devices
      .AsNoTracking()
      .FirstOrDefaultAsync(x => x.Id == request.DeviceId, cancellationToken);

    if (device is null)
    {
      logger.LogWarning("Device {DeviceId} not found.", request.DeviceId);
      return NotFound();
    }

    var authResult = await authorizationService.AuthorizeAsync(
      User,
      device,
      DeviceAccessByDeviceResourcePolicy.PolicyName);

    if (!authResult.Succeeded)
    {
      logger.LogCritical("Authorization failed for user {UserName} on device {DeviceId}.",
        User.Identity?.Name, request.DeviceId);
      return Forbid();
    }

    if (!device.IsOnline)
    {
      logger.LogWarning("Device {DeviceId} is not online.", request.DeviceId);
      return BadRequest("Device is not currently online.");
    }

    var streamId = Guid.NewGuid();
    using var signaler = hubStreamStore.GetOrCreate<InternalDtos.FileSystemEntryDto[]>(streamId);
    try
    {
      var streamRequest = new SubdirectoriesStreamRequestHubDto(streamId, request.DeviceId, request.DirectoryPath);
      var result = await agentHub.Clients.Client(device.ConnectionId)
        .StreamSubdirectories(streamRequest);

      if (!result.IsSuccess)
      {
        logger.LogWarning("Failed to initiate subdirectories stream for device {DeviceId} path {DirectoryPath}: {Reason}",
          request.DeviceId, request.DirectoryPath, result.Reason);
        return BadRequest(result.Reason);
      }

      var entries = new List<InternalDtos.FileSystemEntryDto>();
      await foreach (var chunk in signaler.Reader.ReadAllAsync(cancellationToken))
      {
        entries.AddRange(chunk);
      }

      return Ok(new InternalDtos.GetSubdirectoriesResponseDto(entries.ToArray()));
    }
    catch (OperationCanceledException)
    {
      logger.LogWarning("Subdirectories stream canceled/timed out for device {DeviceId} path {DirectoryPath}", request.DeviceId, request.DirectoryPath);
      return StatusCode(StatusCodes.Status408RequestTimeout);
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error while streaming subdirectories for device {DeviceId} path {DirectoryPath}", request.DeviceId, request.DirectoryPath);
      return StatusCode(500, "An error occurred while retrieving subdirectories.");
    }
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
      DeviceAccessByDeviceResourcePolicy.PolicyName);

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
  public async Task<IActionResult> ValidateFilePath(
    [FromRoute] Guid deviceId,
    [FromBody] InternalDtos.ValidateFilePathRequestDto request,
    [FromServices] AppDb appDb,
    [FromServices] IHubContext<AgentHub, IAgentHubClient> agentHub,
    [FromServices] IAuthorizationService authorizationService,
    [FromServices] ILogger<DeviceFileSystemController> logger,
    CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(request.DirectoryPath) || string.IsNullOrWhiteSpace(request.FileName))
    {
      return BadRequest("Directory path and file name are required.");
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
      DeviceAccessByDeviceResourcePolicy.PolicyName);

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

    var validateRequest = new ValidateFilePathHubDto(request.DirectoryPath, request.FileName);

    try
    {
      var result = await agentHub.Clients
        .Client(device.ConnectionId)
        .ValidateFilePath(validateRequest);

      logger.LogInformation(
        "File path validation completed for {FileName} in {DirectoryPath} on device {DeviceId}: {IsValid}",
        request.FileName, request.DirectoryPath, deviceId, result.IsValid);

      return Ok(result);
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error validating file path {FileName} in {DirectoryPath} on device {DeviceId}",
        request.FileName, request.DirectoryPath, deviceId);
      return StatusCode(500, "An error occurred while validating the file path.");
    }
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
    if (request.TargetPaths is null || request.TargetPaths.Length == 0)
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
      DeviceAccessByDeviceResourcePolicy.PolicyName);

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
    var downloadRequest = new FileArchiveDownloadHubDto(streamId, archiveFileName, request.TargetPaths);

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
        request.TargetPaths.Length);

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
