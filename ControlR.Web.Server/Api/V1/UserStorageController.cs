using Asp.Versioning;
using ControlR.Web.Server.Services.Settings;
using Microsoft.AspNetCore.Mvc;
using StorageDtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.UserStorage;

namespace ControlR.Web.Server.Api.V1;

/// <summary>
/// Per-user key-value storage owned by the calling user. Caller-owned like the other
/// self-service surfaces: tenantId is required on every operation for V1 convention
/// uniformity, and principals without a user id claim cannot use the surface. Get of an
/// unset key answers 204; delete of an unset key answers 404.
/// </summary>
[Route(HttpConstants.V1.UserStorageEndpoint)]
[ApiController]
[Authorize]
[ApiVersion(ApiVersions.V1)]
public class UserStorageController : ControllerBase
{
  [HttpDelete("{key}")]
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<IActionResult> DeleteItem(
    [FromServices] IUserStorageManager userStorageManager,
    [FromRoute] string key,
    [FromQuery] Guid tenantId,
    CancellationToken cancellationToken)
  {
    if (!User.TryResolveTenantId(tenantId, out _))
    {
      return Forbid();
    }

    if (!User.TryGetUserId(out var userId))
    {
      return Unauthorized();
    }

    var deleted = await userStorageManager.Delete(key, userId, cancellationToken);
    return deleted ? NoContent() : NotFound();
  }

  [HttpGet("{key}")]
  [ProducesResponseType<StorageDtos.UserStorageResponseDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  public async Task<ActionResult<StorageDtos.UserStorageResponseDto>> GetItem(
    [FromServices] IUserStorageManager userStorageManager,
    [FromRoute] string key,
    [FromQuery] Guid tenantId,
    CancellationToken cancellationToken)
  {
    if (!User.TryResolveTenantId(tenantId, out _))
    {
      return Forbid();
    }

    if (!User.TryGetUserId(out var userId))
    {
      return Unauthorized();
    }

    var value = await userStorageManager.Get(key, userId, cancellationToken);
    if (value is null)
    {
      return NoContent();
    }

    return Ok(ToV1Dto(value));
  }

  [HttpPost]
  [ProducesResponseType<StorageDtos.UserStorageResponseDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  public async Task<ActionResult<StorageDtos.UserStorageResponseDto>> SetItem(
    [FromServices] IUserStorageManager userStorageManager,
    [FromQuery] Guid tenantId,
    [FromBody] StorageDtos.UserStorageRequestDto request,
    CancellationToken cancellationToken)
  {
    if (!User.TryResolveTenantId(tenantId, out _))
    {
      return Forbid();
    }

    if (!User.TryGetUserId(out var userId))
    {
      return Unauthorized();
    }

    var result = await userStorageManager.Set(request.Key, request.Value, userId, cancellationToken);
    return Ok(ToV1Dto(result));
  }

  private static StorageDtos.UserStorageResponseDto ToV1Dto(InternalDtos.UserStorageResponseDto item)
  {
    return new StorageDtos.UserStorageResponseDto(item.Key, item.Value);
  }
}
