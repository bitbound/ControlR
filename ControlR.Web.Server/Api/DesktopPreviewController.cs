using ControlR.Libraries.Shared.Constants;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace ControlR.Web.Server.Api;

[Route(HttpConstants.DesktopPreviewEndpoint)]
[ApiController]
[Authorize]
public class DesktopPreviewController : ControllerBase
{
  [HttpGet("{deviceId:guid}/{sessionId:int}")]
  public async Task<IActionResult> GetDesktopPreview(
    [FromRoute]Guid deviceId,
    [FromRoute] int sessionId,
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
    using var signaler = hubStreamStore.GetOrCreate(streamId, TimeSpan.FromMinutes(10));
    var desktopPreviewRequestDto = new DesktopPreviewRequestDto()

    agentHub.Clients
      .Client(device.ConnectionId)
      .RequestDesktopPreview(desktopPreviewRequestDto);

    try
    {
      await signaler.ReadySignal.Wait(cancellationToken);
    }
    catch (OperationCanceledException)
    {
      logger.LogWarning("Desktop preview request for device {DeviceId} timed out or was canceled.", deviceId);
      return StatusCode(StatusCodes.Status408RequestTimeout);
    }

    return Ok();
  }
}