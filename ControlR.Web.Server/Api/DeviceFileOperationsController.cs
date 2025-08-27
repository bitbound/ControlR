using ControlR.Libraries.Shared.Constants;
using ControlR.Libraries.Shared.Dtos.HubDtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace ControlR.Web.Server.Api;

[Route(HttpConstants.DeviceFileOperationsEndpoint)]
[ApiController]
[Authorize]
public class FileOperationsController : ControllerBase
{
  [HttpPost("upload")]
  public async Task<IActionResult> UploadFile(
    [FromForm] IFormFile file,
    [FromForm] Guid deviceId,
    [FromForm] string targetDirectory,
    [FromServices] AppDb appDb,
    [FromServices] IHubContext<AgentHub, IAgentHubClient> agentHub,
    [FromServices] IHubStreamStore hubStreamStore,
    [FromServices] IAuthorizationService authorizationService,
    [FromServices] ILogger<FileOperationsController> logger,
    CancellationToken cancellationToken)
  {
    if (file is null || file.Length == 0)
    {
      return BadRequest("No file provided.");
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
    using var signaler = hubStreamStore.GetOrCreate(streamId, TimeSpan.FromMinutes(30));

    var uploadRequest = new FileUploadHubDto(
      streamId,
      targetDirectory,
      file.FileName,
      file.Length);

    try
    {
      // Create chunks and stream to agent
      using var fileStream = file.OpenReadStream();
      var buffer = new byte[30 * 1024]; // 30KB chunks to respect SignalR limits

      var chunks = CreateFileChunks(fileStream, buffer);
      signaler.SetStream(chunks, device.ConnectionId);

      // Notify the agent about the incoming upload
      var receiveResult = await agentHub.Clients
        .Client(device.ConnectionId)
        .ReceiveFileUpload(uploadRequest);

      // Signal completion
      signaler.EndSignal.Set();

      if (receiveResult is null || !receiveResult.IsSuccess)
      {
        logger.LogWarning("Failed to upload file {FileName} to device {DeviceId}: {Reason}",
          file.FileName, deviceId, receiveResult?.Reason ?? "Unknown error");
        return BadRequest(receiveResult?.Reason ?? "File upload failed.");
      }
      
      logger.LogInformation("File upload completed for {FileName} to device {DeviceId}", 
        file.FileName, deviceId);
      
      return Ok("File uploaded successfully");
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

  [HttpGet("download/{deviceId:guid}")]
  public async Task<IActionResult> DownloadFile(
    [FromRoute] Guid deviceId,
    [FromQuery] string filePath,
    [FromServices] AppDb appDb,
    [FromServices] IHubContext<AgentHub, IAgentHubClient> agentHub,
    [FromServices] IHubStreamStore hubStreamStore,
    [FromServices] IAuthorizationService authorizationService,
    [FromServices] ILogger<FileOperationsController> logger,
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
    using var signaler = hubStreamStore.GetOrCreate(streamId, TimeSpan.FromMinutes(30));

    var downloadRequest = new FileDownloadHubDto(
      streamId,
      filePath,
      false); // IsDirectory - we'll detect this on the agent side

    try
    {
      // Request the file from the agent
      await agentHub.Clients
        .Client(device.ConnectionId)
        .SendFileDownload(downloadRequest);

      // Wait for the agent to start streaming
      await signaler.ReadySignal.Wait(cancellationToken);

      if (signaler.Stream is null)
      {
        logger.LogWarning("No stream available for file download from device {DeviceId}.", deviceId);
        return StatusCode(StatusCodes.Status404NotFound);
      }

      // Determine file name for download
      var fileName = Path.GetFileName(filePath);
      if (string.IsNullOrWhiteSpace(fileName))
      {
        fileName = "download";
      }

      // Set response headers for file download
      Response.ContentType = "application/octet-stream";
      Response.Headers.Append("Content-Disposition", $"attachment; filename=\"{fileName}\"");

      // Stream the file content to the response
      await foreach (var chunk in signaler.Stream.WithCancellation(cancellationToken))
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

  [HttpDelete("delete/{deviceId:guid}")]
  public async Task<IActionResult> DeleteFile(
    [FromRoute] Guid deviceId,
    [FromBody] FileDeleteRequestDto request,
    [FromServices] AppDb appDb,
    [FromServices] IHubContext<AgentHub, IAgentHubClient> agentHub,
    [FromServices] IAuthorizationService authorizationService,
    [FromServices] ILogger<FileOperationsController> logger,
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

    var deleteRequest = new FileDeleteHubDto(
      request.FilePath,
      false); // IsDirectory - agent will handle detection

    try
    {
      await agentHub.Clients
        .Client(device.ConnectionId)
        .DeleteFile(deleteRequest);

      logger.LogInformation("File deletion requested for {FilePath} on device {DeviceId}", 
        request.FilePath, deviceId);

      return Ok(new { Message = "File deletion completed", FilePath = request.FilePath });
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error deleting file {FilePath} on device {DeviceId}", 
        request.FilePath, deviceId);
      return StatusCode(500, "An error occurred during file deletion.");
    }
  }

  private static async IAsyncEnumerable<byte[]> CreateFileChunks(
    Stream fileStream, 
    byte[] buffer)
  {
    int bytesRead;
    while ((bytesRead = await fileStream.ReadAsync(buffer)) > 0)
    {
      if (bytesRead == buffer.Length)
      {
        yield return buffer;
      }
      else
      {
        // Create a properly sized array for the last chunk
        var finalChunk = new byte[bytesRead];
        Array.Copy(buffer, finalChunk, bytesRead);
        yield return finalChunk;
      }
    }
  }
}
