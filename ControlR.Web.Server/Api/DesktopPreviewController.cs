using ControlR.Libraries.Shared.Constants;
using ControlR.Libraries.Shared.Dtos.HubDtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace ControlR.Web.Server.Api;

[Route(HttpConstants.DesktopPreviewEndpoint)]
[ApiController]
[Authorize]
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
    [FromServices] ILogger<DesktopPreviewController> logger,
    CancellationToken cancellationToken)
  {
    var device = await appDb.Devices
      .AsNoTracking()
      .FirstOrDefaultAsync(x => x.Id == deviceId);

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
      logger.LogCritical("Authorization failed for user {UserName} on device {DeviceId}.", User.Identity?.Name, deviceId);
      return Forbid();
    }

    var requesterId = Guid.NewGuid();
    var streamId = Guid.NewGuid();
    using var signaler = hubStreamStore.GetOrCreate<byte[]>(streamId, TimeSpan.FromMinutes(10));

    var desktopPreviewRequestDto = new DesktopPreviewRequestDto(
      requesterId,
      streamId,
      targetProcessId);

    await agentHub.Clients
      .Client(device.ConnectionId)
      .RequestDesktopPreview(desktopPreviewRequestDto);

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
      logger.LogWarning("Desktop preview request for device {DeviceId} timed out or was canceled.", deviceId);
      return StatusCode(StatusCodes.Status408RequestTimeout);
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error streaming desktop preview for device {DeviceId}.", deviceId);
      return StatusCode(StatusCodes.Status500InternalServerError);
    }
  }
}