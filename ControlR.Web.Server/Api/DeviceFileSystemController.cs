using ControlR.Libraries.Shared.Constants;
using ControlR.Libraries.Shared.Dtos.HubDtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace ControlR.Web.Server.Api;

[Route(HttpConstants.DeviceFileSystemEndpoint)]
[ApiController]
[Authorize]
public class DeviceFileSystemController : ControllerBase
{
  [HttpPost("contents")]
  public async Task<IActionResult> GetDirectoryContents(
    [FromBody] GetDirectoryContentsRequestDto request,
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

    if (string.IsNullOrWhiteSpace(device.ConnectionId))
    {
      logger.LogWarning("Device {DeviceId} is not connected (no ConnectionId).", request.DeviceId);
      return BadRequest("Device is not currently connected.");
    }

    var streamId = Guid.NewGuid();
    using var signaler = hubStreamStore.GetOrCreate<FileSystemEntryDto[]>(streamId, TimeSpan.FromMinutes(5));
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

      var entries = new List<FileSystemEntryDto>();
      await foreach (var chunk in signaler.Reader.ReadAllAsync(cancellationToken))
      {
        entries.AddRange(chunk);
      }

      var directoryExists = signaler.Metadata is bool b && b;
      return Ok(new GetDirectoryContentsResponseDto([.. entries], directoryExists));
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

  [HttpPost("path-segments")]
  public async Task<IActionResult> GetPathSegments(
    [FromBody] GetPathSegmentsRequestDto request,
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
    [FromBody] GetRootDrivesRequestDto request,
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

    if (string.IsNullOrWhiteSpace(device.ConnectionId))
    {
      logger.LogWarning("Device {DeviceId} is not connected (no ConnectionId).", request.DeviceId);
      return BadRequest("Device is not currently connected.");
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
    [FromBody] GetSubdirectoriesRequestDto request,
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

    if (string.IsNullOrWhiteSpace(device.ConnectionId))
    {
      logger.LogWarning("Device {DeviceId} is not connected (no ConnectionId).", request.DeviceId);
      return BadRequest("Device is not currently connected.");
    }

    var streamId = Guid.NewGuid();
    using var signaler = hubStreamStore.GetOrCreate<FileSystemEntryDto[]>(streamId, TimeSpan.FromMinutes(5));
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

      var entries = new List<FileSystemEntryDto>();
      await foreach (var chunk in signaler.Reader.ReadAllAsync(cancellationToken))
      {
        entries.AddRange(chunk);
      }

      return Ok(new GetSubdirectoriesResponseDto(entries.ToArray()));
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
}