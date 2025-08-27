using ControlR.Libraries.Shared.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace ControlR.Web.Server.Api;

[Route(HttpConstants.DeviceFileSystemEndpoint)]
[ApiController]
[Authorize]
public class DeviceFileSystemController : ControllerBase
{
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
        .GetSubdirectories(request);

      if (result.IsSuccess)
      {
        return Ok(result.Value);
      }

      logger.LogWarning("Failed to get subdirectories for device {DeviceId} path {DirectoryPath}: {Reason}", 
        request.DeviceId, request.DirectoryPath, result.Reason);
      return BadRequest(result.Reason);
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error while getting subdirectories for device {DeviceId} path {DirectoryPath}", 
        request.DeviceId, request.DirectoryPath);
      return StatusCode(500, "An error occurred while retrieving subdirectories.");
    }
  }

  [HttpPost("contents")]
  public async Task<IActionResult> GetDirectoryContents(
    [FromBody] GetDirectoryContentsRequestDto request,
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
        .GetDirectoryContents(request);

      if (result.IsSuccess)
      {
        return Ok(result.Value);
      }

      logger.LogWarning("Failed to get directory contents for device {DeviceId} path {DirectoryPath}: {Reason}", 
        request.DeviceId, request.DirectoryPath, result.Reason);
      return BadRequest(result.Reason);
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error while getting directory contents for device {DeviceId} path {DirectoryPath}", 
        request.DeviceId, request.DirectoryPath);
      return StatusCode(500, "An error occurred while retrieving directory contents.");
    }
  }
}