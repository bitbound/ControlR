using Asp.Versioning;
using ControlR.Web.Server.Authz.Permissions;
using Microsoft.AspNetCore.Mvc;
using PATDtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.PersonalAccessTokens;

namespace ControlR.Web.Server.Api.V1;

/// <summary>
/// Self-service personal access tokens for the calling user. The tokens are always owned by
/// the caller, so no resource is addressed by id across principals. TenantId stays required on
/// every operation to keep the V1 convention uniform (server principals resolve the tenant
/// check but then fail the caller-has-no-AppUser lookup). Create returns 201 and the list
/// returns an Items envelope. Delete answers 204.
/// </summary>
[Route(HttpConstants.V1.PersonalAccessTokensEndpoint)]
[ApiController]
[Authorize]
[ApiVersion(ApiVersions.V1)]
public class PersonalAccessTokensController : ControllerBase
{
  [HttpPost]
  [Authorize(Policy = PolicyNames.RequirePersonalAccessTokenSelfWrite)]
  [ProducesResponseType<PATDtos.CreatePersonalAccessTokenResponseDto>(StatusCodes.Status201Created)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  public async Task<ActionResult<PATDtos.CreatePersonalAccessTokenResponseDto>> Create(
    [FromServices] IPersonalAccessTokenManager personalAccessTokenManager,
    [FromServices] UserManager<AppUser> userManager,
    [FromQuery] Guid tenantId,
    [FromBody] PATDtos.CreatePersonalAccessTokenRequestDto request)
  {
    if (!User.TryResolveTenantId(tenantId, out var resolvedTenantId))
    {
      return Forbid();
    }

    var user = await userManager.GetUserAsync(User);
    if (user is null || user.TenantId == Guid.Empty)
    {
      return BadRequest("User tenant not found");
    }

    if (User.ToPrincipalDescriptor() is not { } actor)
    {
      return BadRequest("User ID not found.");
    }

    var result = await personalAccessTokenManager.CreateToken(
      new InternalDtos.CreatePersonalAccessTokenRequestDto(
        request.Name,
        request.PermissionMode,
        request.Scopes
          ?.Select(scope => new InternalDtos.CredentialScopeDto(
            scope.PermissionName,
            scope.ScopeKind,
            scope.ScopeId))
          .ToList()),
      user.Id,
      actor);

    if (!result.IsSuccess)
    {
      return BadRequest(result.Reason);
    }

    var response = new PATDtos.CreatePersonalAccessTokenResponseDto(
      ToV1ResponseDto(result.Value.PersonalAccessToken),
      result.Value.PlainTextToken);

    return CreatedAtAction(
      nameof(GetAll),
      new { tenantId = resolvedTenantId },
      response);
  }

  [HttpDelete("{id:guid}")]
  [Authorize(Policy = PolicyNames.RequirePersonalAccessTokenSelfWrite)]
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  public async Task<IActionResult> Delete(
    [FromServices] IPersonalAccessTokenManager personalAccessTokenManager,
    [FromServices] UserManager<AppUser> userManager,
    [FromRoute] Guid id,
    [FromQuery] Guid tenantId)
  {
    if (!User.TryResolveTenantId(tenantId, out _))
    {
      return Forbid();
    }

    var user = await userManager.GetUserAsync(User);
    if (user is null)
    {
      return BadRequest("User not found.");
    }

    var result = await personalAccessTokenManager.Delete(id, user.Id);
    if (!result.IsSuccess)
    {
      return BadRequest(result.Reason);
    }

    return NoContent();
  }

  [HttpGet]
  [Authorize(Policy = PolicyNames.RequirePersonalAccessTokenSelfRead)]
  [ProducesResponseType<PATDtos.PersonalAccessTokensResponseDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  public async Task<ActionResult<PATDtos.PersonalAccessTokensResponseDto>> GetAll(
    [FromServices] IPersonalAccessTokenManager personalAccessTokenManager,
    [FromServices] UserManager<AppUser> userManager,
    [FromQuery] Guid tenantId)
  {
    if (!User.TryResolveTenantId(tenantId, out _))
    {
      return Forbid();
    }

    var user = await userManager.GetUserAsync(User);
    if (user is null)
    {
      return BadRequest("User not found.");
    }

    var tokens = await personalAccessTokenManager.GetForUser(user.Id);
    return Ok(new PATDtos.PersonalAccessTokensResponseDto
    {
      Items = [.. tokens.Select(ToV1ResponseDto)]
    });
  }

  [HttpPut("{id:guid}")]
  [Authorize(Policy = PolicyNames.RequirePersonalAccessTokenSelfWrite)]
  [ProducesResponseType<PATDtos.PersonalAccessTokenResponseDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  public async Task<ActionResult<PATDtos.PersonalAccessTokenResponseDto>> Update(
    [FromServices] IPersonalAccessTokenManager personalAccessTokenManager,
    [FromServices] UserManager<AppUser> userManager,
    [FromRoute] Guid id,
    [FromQuery] Guid tenantId,
    [FromBody] PATDtos.UpdatePersonalAccessTokenRequestDto request)
  {
    if (!User.TryResolveTenantId(tenantId, out _))
    {
      return Forbid();
    }

    var user = await userManager.GetUserAsync(User);
    if (user is null)
    {
      return BadRequest("User not found.");
    }

    var result = await personalAccessTokenManager.Update(
      id,
      new InternalDtos.UpdatePersonalAccessTokenRequestDto(request.Name),
      user.Id);

    if (!result.IsSuccess)
    {
      return BadRequest(result.Reason);
    }

    return Ok(ToV1ResponseDto(result.Value));
  }

  private static PATDtos.PersonalAccessTokenResponseDto ToV1ResponseDto(
    InternalDtos.PersonalAccessTokenResponseDto token)
  {
    return new PATDtos.PersonalAccessTokenResponseDto(
      token.Id,
      token.Name,
      token.CreatedAt,
      token.LastUsed,
      token.PermissionCount,
      token.PermissionMode);
  }
}
