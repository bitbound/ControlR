using ControlR.Libraries.Shared.Constants;
using ControlR.Libraries.Shared.Dtos.HubDtos;
using ControlR.Libraries.Shared.Hubs.Clients;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Net.Http.Headers;

namespace ControlR.Web.Server.Api;

[Route(HttpConstants.DeviceFileOperationsEndpoint)]
[ApiController]
[Authorize]
public class DeviceFileOperationsController : ControllerBase
{
  [HttpPost("create-directory/{deviceId:guid}")]
  public async Task<IActionResult> CreateDirectory(
    [FromRoute] Guid deviceId,
    [FromBody] CreateDirectoryRequestDto request,
    [FromServices] AppDb appDb,
    [FromServices] IHubContext<AgentHub, IAgentHubClient> agentHub,
    [FromServices] IAuthorizationService authorizationService,
    [FromServices] ILogger<DeviceFileOperationsController> logger,
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

    if (string.IsNullOrWhiteSpace(device.ConnectionId))
    {
      logger.LogWarning("Device {DeviceId} is not connected (no ConnectionId).", deviceId);
      return BadRequest("Device is not currently connected.");
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

  [HttpDelete("delete/{deviceId:guid}")]
  public async Task<IActionResult> DeleteFile(
    [FromRoute] Guid deviceId,
    [FromBody] FileDeleteRequestDto request,
    [FromServices] AppDb appDb,
    [FromServices] IHubContext<AgentHub, IAgentHubClient> agentHub,
    [FromServices] IAuthorizationService authorizationService,
    [FromServices] ILogger<DeviceFileOperationsController> logger,
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

    if (string.IsNullOrWhiteSpace(device.ConnectionId))
    {
      logger.LogWarning("Device {DeviceId} is not connected (no ConnectionId).", deviceId);
      return BadRequest("Device is not currently connected.");
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

  [HttpGet("download/{deviceId:guid}")]
  public async Task<IActionResult> DownloadFile(
    [FromRoute] Guid deviceId,
    [FromQuery] string filePath,
    [FromQuery] string? fileName,
    [FromServices] AppDb appDb,
    [FromServices] IHubContext<AgentHub, IAgentHubClient> agentHub,
    [FromServices] IHubStreamStore hubStreamStore,
    [FromServices] IAuthorizationService authorizationService,
    [FromServices] IOptionsMonitor<AppOptions> appOptions,
    [FromServices] ILogger<DeviceFileOperationsController> logger,
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

    if (string.IsNullOrWhiteSpace(device.ConnectionId))
    {
      logger.LogWarning("Device {DeviceId} is not connected (no ConnectionId).", deviceId);
      return BadRequest("Device is not currently connected.");
    }

    var streamId = Guid.NewGuid();
    using var signaler = hubStreamStore.GetOrCreate<byte[]>(streamId, TimeSpan.FromMinutes(30));

    var downloadRequest = new FileDownloadHubDto(streamId, filePath);

    try
    {
      // Request the file from the agent
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
      
      // Set response headers for file download
      Response.ContentType = "application/octet-stream";
      var contentDisposition = new ContentDispositionHeaderValue("attachment");
      contentDisposition.SetHttpFileName(requestResult.Value.FileDisplayName);
      Response.Headers[HeaderNames.ContentDisposition] = contentDisposition.ToString();
      Response.Headers.ContentLength = fileSize;

      // Stream the file content to the response
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

  [HttpPost("validate-path/{deviceId:guid}")]
  public async Task<IActionResult> ValidateFilePath(
    [FromRoute] Guid deviceId,
    [FromBody] ValidateFilePathRequestDto request,
    [FromServices] AppDb appDb,
    [FromServices] IHubContext<AgentHub, IAgentHubClient> agentHub,
    [FromServices] IAuthorizationService authorizationService,
    [FromServices] ILogger<DeviceFileOperationsController> logger,
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

    if (string.IsNullOrWhiteSpace(device.ConnectionId))
    {
      logger.LogWarning("Device {DeviceId} is not connected (no ConnectionId).", deviceId);
      return BadRequest("Device is not currently connected.");
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
}
