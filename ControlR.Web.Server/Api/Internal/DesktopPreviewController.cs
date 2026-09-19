using ControlR.Libraries.Api.Contracts.Dtos.HubDtos;
using ControlR.Libraries.Api.Contracts.Hubs.Clients;
using ControlR.Web.Server.Constants;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace ControlR.Web.Server.Api.Internal;

[Route(HttpConstants.Internal.DesktopPreviewEndpoint)]
[ApiController]
[Authorize]
[EndpointGroupName(OpenApiConstants.InternalGroupName)]
public class DesktopPreviewController : ControllerBase
{
  [HttpGet("{deviceId:guid}/{targetProcessId:int}")]
  public async Task<IActionResult> GetDesktopPreview(
    [FromRoute] Guid deviceId,
    [FromRoute] int targetProcessId,
    [FromServices] AppDb appDb,
    [FromServices] IHubContext<AgentHub, IAgentHubClient> agentHub,
    [FromServices] IHubStreamStore hubStreamStore,
    [FromServices] IAuthorizationService authorizationService,
    [FromServices] IOptionsMonitor<AppOptions> appOptions,
    [FromServices] ILogger<DesktopPreviewController> logger,
    CancellationToken cancellationToken)
  {
    if (appOptions.CurrentValue.DisableDesktopPreview)
    {
      logger.LogWarning(
        "Desktop preview request rejected for device {DeviceId}. Desktop preview is disabled by configuration.",
        deviceId);
      return NotFound();
    }
    var device = await appDb.Devices
      .AsNoTracking()
      .FirstOrDefaultAsync(x => x.Id == deviceId, cancellationToken: cancellationToken);

    if (device is null)
    {
      logger.LogWarning("Device {DeviceId} not found.", deviceId);
      return NotFound();
    }

    var authResult = await authorizationService.AuthorizeAsync(
      User,
      device,
      DeviceResourcePolicies.DesktopPreviewRead);

    if (!authResult.Succeeded)
    {
      logger.LogCritical("Authorization failed for user {UserName} on device {DeviceId}.", User.Identity?.Name, deviceId);
      return Forbid();
    }

    var requesterId = Guid.NewGuid();
    var streamId = Guid.NewGuid();
    using var signaler = hubStreamStore.GetOrCreate<byte[]>(streamId, HubStreamExpiration.DesktopPreview);

    var desktopPreviewRequestDto = new DesktopPreviewRequestDto(
      requesterId,
      streamId,
      targetProcessId);

    logger.LogInformation(
      "Sending desktop preview request for device {DeviceId} and process {TargetProcessId} from user {UserName}.",
      deviceId,
      targetProcessId,
      User.Identity?.Name);

    // An offline device has an empty ConnectionId; SignalR's Clients.Client("") is a no-op that
    // returns default (null), so guard before dereferencing. A device that is not connected cannot
    // serve the request, which is a conflict with its state rather than a server fault.
    if (string.IsNullOrEmpty(device.ConnectionId))
    {
      logger.LogWarning(
        "Desktop preview request for device {DeviceId} rejected: device is not connected.",
        deviceId);
      return Problem(
        detail: "Device is not connected.",
        statusCode: StatusCodes.Status409Conflict,
        title: V1ProblemTitles.Conflict);
    }

    var requestResult = await agentHub.Clients
      .Client(device.ConnectionId)
      .RequestDesktopPreview(desktopPreviewRequestDto);

    if (requestResult is null)
    {
      logger.LogWarning(
        "Desktop preview request for device {DeviceId} and process {TargetProcessId} returned no result.",
        deviceId,
        targetProcessId);

      return Problem(
        detail: "Desktop preview request returned no result.",
        statusCode: StatusCodes.Status503ServiceUnavailable,
        title: V1ProblemTitles.ServiceUnavailable);
    }

    if (!requestResult.IsSuccess)
    {
      logger.LogWarning(
        "Desktop preview request for device {DeviceId} and process {TargetProcessId} failed: {ErrorMessage}",
        deviceId,
        targetProcessId,
        requestResult.Reason);

      return Problem(
        detail: requestResult.Reason,
        statusCode: StatusCodes.Status503ServiceUnavailable,
        title: V1ProblemTitles.ServiceUnavailable);
    }

    try
    {
      // Set response content type for JPEG image
      Response.ContentType = "image/jpeg";

      // Stream the bytes directly to the response
      await foreach (var chunk in signaler.Reader.ReadAllAsync(cancellationToken))
      {
        await Response.Body.WriteAsync(chunk, cancellationToken);
      }

      return new EmptyResult();
    }
    catch (OperationCanceledException)
    {
      logger.LogError("Desktop preview request for device {DeviceId} timed out or was canceled.", deviceId);
      throw;
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error streaming desktop preview for device {DeviceId}.", deviceId);
      throw;
    }
  }
}