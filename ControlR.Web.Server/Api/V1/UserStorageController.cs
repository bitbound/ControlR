using Asp.Versioning;
using ControlR.Web.Server.Services.Settings;
using Microsoft.AspNetCore.Mvc;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.UserStorage;

namespace ControlR.Web.Server.Api.V1;

/// <summary>
/// Per-user key-value storage owned by the calling user. Every operation requires a tenantId
/// per the V1 convention.
/// </summary>
[Route(HttpConstants.V1.UserStorageEndpoint)]
[ApiController]
[Authorize]
[ApiVersion(ApiVersions.V1)]
public class UserStorageController : ControllerBase
{
  [HttpDelete("{key}")]
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
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
  [ProducesResponseType<UserStorageResponseDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
  public async Task<ActionResult<UserStorageResponseDto>> GetItem(
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
  [ProducesResponseType<UserStorageResponseDto>(StatusCodes.Status201Created)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
  public async Task<ActionResult<UserStorageResponseDto>> SetItem(
    [FromServices] IUserStorageManager userStorageManager,
    [FromQuery] Guid tenantId,
    [FromBody] UserStorageRequestDto request,
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
    var dto = ToV1Dto(result);
    return CreatedAtAction(nameof(GetItem), new { key = dto.Key, tenantId }, dto);
  }

  private static UserStorageResponseDto ToV1Dto(InternalDtos.UserStorageResponseDto item)
  {
    return new UserStorageResponseDto(item.Key, item.Value);
  }
}
