using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.DeviceTags;
using ControlR.Web.Server.Constants;

namespace ControlR.Web.Server.Api.V1;

/// <summary>
/// Adding and removing tags on a device. Every operation is tenant-scoped by the required
/// tenantId query parameter.
/// </summary>
[Route(HttpConstants.V1.DeviceTagsEndpoint)]
[ApiController]
[Authorize]
[ApiVersion(ApiVersions.V1)]
public class DeviceTagsController : ControllerBase
{
  [HttpPost]
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
  public async Task<IActionResult> Add(
    [FromServices] AppDb appDb,
    [FromServices] IAuthorizationService authorizationService,
    [FromQuery] Guid tenantId,
    [FromBody] DeviceTagAddRequestDto request,
    CancellationToken cancellationToken)
  {
    if (!User.TryResolveTenantId(tenantId, out var resolvedTenantId))
    {
      return Forbid();
    }

    var device = await appDb.Devices
      .Include(x => x.Tags)
      .FirstOrDefaultAsync(x => x.Id == request.DeviceId && x.TenantId == resolvedTenantId, cancellationToken);

    if (device is null)
    {
      return Problem(statusCode: StatusCodes.Status404NotFound, title: V1ProblemTitles.NotFound);
    }

    var authResult = await authorizationService.AuthorizeAsync(User, device, DeviceResourcePolicies.TagsWrite);
    if (!authResult.Succeeded)
    {
      return Forbid();
    }

    var tag = await appDb.Tags
      .FirstOrDefaultAsync(x => x.Id == request.TagId && x.TenantId == resolvedTenantId, cancellationToken);

    if (tag is null)
    {
      return Problem(statusCode: StatusCodes.Status404NotFound, title: V1ProblemTitles.NotFound);
    }

    device.Tags ??= [];
    device.Tags.Add(tag);
    await appDb.SaveChangesAsync(cancellationToken);

    return NoContent();
  }

  [HttpDelete("{deviceId:guid}/{tagId:guid}")]
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
  public async Task<IActionResult> Remove(
    [FromServices] AppDb appDb,
    [FromServices] IAuthorizationService authorizationService,
    [FromRoute] Guid deviceId,
    [FromRoute] Guid tagId,
    [FromQuery] Guid tenantId,
    CancellationToken cancellationToken)
  {
    if (!User.TryResolveTenantId(tenantId, out var resolvedTenantId))
    {
      return Forbid();
    }

    var device = await appDb.Devices
      .Include(x => x.Tags)
      .FirstOrDefaultAsync(x => x.Id == deviceId && x.TenantId == resolvedTenantId, cancellationToken);

    if (device is null)
    {
      return Problem(statusCode: StatusCodes.Status404NotFound, title: V1ProblemTitles.NotFound);
    }

    var authResult = await authorizationService.AuthorizeAsync(User, device, DeviceResourcePolicies.TagsWrite);
    if (!authResult.Succeeded)
    {
      return Forbid();
    }

    device.Tags ??= [];
    var tag = device.Tags.Find(x => x.Id == tagId);
    if (tag is null)
    {
      return Problem(statusCode: StatusCodes.Status404NotFound, title: V1ProblemTitles.NotFound);
    }

    device.Tags.Remove(tag);
    await appDb.SaveChangesAsync(cancellationToken);

    return NoContent();
  }
}
