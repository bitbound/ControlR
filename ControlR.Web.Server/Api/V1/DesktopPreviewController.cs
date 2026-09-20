using Asp.Versioning;
using ControlR.Libraries.Api.Contracts.Dtos.HubDtos;
using ControlR.Libraries.Api.Contracts.Hubs.Clients;
using ControlR.Web.Server.Constants;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace ControlR.Web.Server.Api.V1;

/// <summary>
/// Desktop preview for the versioned API. The response is the image the agent captured, and every
/// failure is a problem document.
/// </summary>
[Route(HttpConstants.V1.DesktopPreviewEndpoint)]
[ApiController]
[Authorize]
[ApiVersion(ApiVersions.V1)]
public class DesktopPreviewController(
  AppDb appDb,
  IHubContext<AgentHub, IAgentHubClient> agentHub,
  IHubStreamStore hubStreamStore,
  IAuthorizationService authorizationService,
  IOptionsMonitor<AppOptions> appOptions,
  ILogger<DesktopPreviewController> logger) : ControllerBase
{
  private readonly IHubContext<AgentHub, IAgentHubClient> _agentHub = agentHub;
  private readonly AppDb _appDb = appDb;
  private readonly IOptionsMonitor<AppOptions> _appOptions = appOptions;
  private readonly IAuthorizationService _authorizationService = authorizationService;
  private readonly IHubStreamStore _hubStreamStore = hubStreamStore;
  private readonly ILogger<DesktopPreviewController> _logger = logger;

  /// <summary>
  /// Captures one frame of a process's desktop on the device named by the route. The answer is a
  /// single image, so there is no length to state up front.
  /// </summary>
  [HttpGet("{deviceId:guid}/{targetProcessId:int}")]
  [DisableRequestTimeout]
  [BinaryResponse("image/jpeg")]
  [ProducesResponseType(StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status408RequestTimeout, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway, "application/problem+json")]
  public async Task<IActionResult> GetDesktopPreview(
    [FromRoute] Guid deviceId,
    [FromRoute] int targetProcessId,
    [FromQuery] Guid tenantId,
    CancellationToken cancellationToken)
  {
    if (!User.TryResolveTenantId(tenantId, out var resolvedTenantId))
    {
      return Forbid();
    }

    if (_appOptions.CurrentValue.DisableDesktopPreview)
    {
      _logger.LogWarning(
        "Desktop preview request rejected for device {DeviceId}. Desktop preview is disabled by configuration.",
        deviceId);

      return Problem(
        statusCode: StatusCodes.Status404NotFound,
        title: V1ProblemTitles.NotFound);
    }

    var device = await _appDb.Devices
      .AsNoTracking()
      .FirstOrDefaultAsync(
        x => x.Id == deviceId && x.TenantId == resolvedTenantId,
        cancellationToken);

    if (device is null)
    {
      _logger.LogWarning("Device {DeviceId} not found.", deviceId);
      return Problem(
        statusCode: StatusCodes.Status404NotFound,
        title: V1ProblemTitles.NotFound);
    }

    var authResult = await _authorizationService.AuthorizeAsync(
      User,
      device,
      DeviceResourcePolicies.DesktopPreviewRead);

    if (!authResult.Succeeded)
    {
      _logger.LogCritical("Authorization failed for user {UserName} on device {DeviceId}.",
        User.Identity?.Name, deviceId);
      return Forbid();
    }

    // IsOnline is what states the device cannot serve this, which is a conflict with its state rather
    // than a server fault. The connection id is checked too because the hub call below addresses it,
    // and SignalR's Clients.Client("") is a no-op that returns nothing.
    if (!device.IsOnline || string.IsNullOrWhiteSpace(device.ConnectionId))
    {
      _logger.LogWarning(
        "Desktop preview request for device {DeviceId} rejected: device is offline.",
        deviceId);

      return Problem(
        detail: "Device is currently offline.",
        statusCode: StatusCodes.Status409Conflict,
        title: V1ProblemTitles.Conflict);
    }

    var streamId = Guid.NewGuid();
    using var signaler = _hubStreamStore.GetOrCreate<byte[]>(streamId, HubStreamExpiration.DesktopPreview);

    var previewRequest = new DesktopPreviewRequestDto(
      Guid.NewGuid(),
      streamId,
      targetProcessId);

    _logger.LogInformation(
      "Sending desktop preview request for device {DeviceId} and process {TargetProcessId} from user {UserName}.",
      deviceId,
      targetProcessId,
      User.Identity?.Name);

    var result = await _agentHub.Clients
      .Client(device.ConnectionId)
      .RequestDesktopPreview(previewRequest);

    if (result is null)
    {
      _logger.LogWarning(
        "Desktop preview request for device {DeviceId} and process {TargetProcessId} returned no result.",
        deviceId,
        targetProcessId);

      // The hub call answered with nothing, which is the agent's "did not respond" condition. The
      // server itself was reachable, so the answer is 502 upstream-unreachable rather than 503
      // server-unavailable, matching MapFailure's NoResponse mapping.
      return Problem(
        detail: "The device did not return a result.",
        statusCode: StatusCodes.Status502BadGateway,
        title: V1ProblemTitles.BadGateway);
    }

    if (!result.IsSuccess)
    {
      _logger.LogWarning(
        "Desktop preview request for device {DeviceId} and process {TargetProcessId} failed: {ErrorMessage}",
        deviceId,
        targetProcessId,
        result.Reason);

      // The hub call answered with a refusal, which is the agent reporting a conflict with the state
      // on the device (no interactive session, no capture permission, etc.). MapFailure's RemoteFailure
      // answers 409 with the agent's reason as detail, so the preview follows the same convention
      // instead of telling the caller to retry a state that will not fix itself.
      return Problem(
        detail: result.Reason,
        statusCode: StatusCodes.Status409Conflict,
        title: V1ProblemTitles.Conflict);
    }

    Response.ContentType = "image/jpeg";

    try
    {
      await foreach (var chunk in signaler.Reader.ReadAllAsync(cancellationToken))
      {
        if (chunk.Length > 0)
        {
          await Response.Body.WriteAsync(chunk, cancellationToken);
        }
      }

      return new EmptyResult();
    }
    catch (OperationCanceledException) when (!Response.HasStarted)
    {
      return Problem(
        detail: "The wait for the remote device was canceled.",
        statusCode: StatusCodes.Status408RequestTimeout,
        title: V1ProblemTitles.RequestTimedOut);
    }
    catch (Exception ex) when (!Response.HasStarted)
    {
      _logger.LogError(ex, "Error streaming desktop preview for device {DeviceId}.", deviceId);

      return Problem(
        detail: "An error occurred while streaming the preview from the device.",
        statusCode: StatusCodes.Status500InternalServerError,
        title: V1ProblemTitles.InternalServerError);
    }
  }
}
