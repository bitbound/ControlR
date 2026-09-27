using Asp.Versioning;
using ControlR.Web.Server.Authz.Permissions;
using Microsoft.AspNetCore.Mvc;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.PersonalAccessTokens;
using ControlR.Web.Server.Constants;

namespace ControlR.Web.Server.Api.V1;

/// <summary>
/// Self-service personal access tokens for the calling user. The tokens are always caller-owned,
/// and every operation requires a tenantId per the V1 convention.
/// </summary>
[Route(HttpConstants.V1.PersonalAccessTokensEndpoint)]
[ApiController]
[Authorize]
[ApiVersion(ApiVersions.V1)]
public class PersonalAccessTokensController : ControllerBase
{
  [HttpPost]
  [Authorize(Policy = PolicyNames.RequirePersonalAccessTokenSelfWrite)]
  [ProducesResponseType<CreatePersonalAccessTokenResponseDto>(StatusCodes.Status201Created)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
  public async Task<ActionResult<CreatePersonalAccessTokenResponseDto>> Create(
    [FromServices] IPersonalAccessTokenManager personalAccessTokenManager,
    [FromServices] UserManager<AppUser> userManager,
    [FromQuery] Guid tenantId,
    [FromBody] CreatePersonalAccessTokenRequestDto request)
  {
    if (!User.TryResolveTenantId(tenantId, out var resolvedTenantId))
    {
      return Forbid();
    }

    var user = await userManager.GetUserAsync(User);
    if (user is null || user.TenantId == Guid.Empty)
    {
      return Problem(
        detail: "User tenant not found",
        statusCode: StatusCodes.Status400BadRequest,
        title: V1ProblemTitles.InvalidRequest);
    }

    if (User.ToPrincipalDescriptor() is not { } actor)
    {
      return Problem(
        detail: "User ID not found.",
        statusCode: StatusCodes.Status400BadRequest,
        title: V1ProblemTitles.InvalidRequest);
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
          .ToList(),
        request.ExpiresAt),
      user.Id,
      actor);

    if (!result.IsSuccess)
    {
      return Problem(
        detail: result.Reason,
        statusCode: StatusCodes.Status400BadRequest,
        title: V1ProblemTitles.InvalidRequest);
    }

    var response = new CreatePersonalAccessTokenResponseDto(
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
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
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
      return Problem(
        detail: "User not found.",
        statusCode: StatusCodes.Status400BadRequest,
        title: V1ProblemTitles.InvalidRequest);
    }

    var result = await personalAccessTokenManager.Delete(id, user.Id);
    if (!result.IsSuccess)
    {
      return Problem(
        detail: result.Reason,
        statusCode: StatusCodes.Status400BadRequest,
        title: V1ProblemTitles.InvalidRequest);
    }

    return NoContent();
  }

  [HttpGet]
  [Authorize(Policy = PolicyNames.RequirePersonalAccessTokenSelfRead)]
  [ProducesResponseType<PersonalAccessTokensResponseDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
  public async Task<ActionResult<PersonalAccessTokensResponseDto>> GetAll(
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
      return Problem(
        detail: "User not found.",
        statusCode: StatusCodes.Status400BadRequest,
        title: V1ProblemTitles.InvalidRequest);
    }

    var tokens = await personalAccessTokenManager.GetForUser(user.Id);
    return Ok(new PersonalAccessTokensResponseDto
    {
      Items = [.. tokens.Select(ToV1ResponseDto)]
    });
  }

  [HttpPost("{id:guid}/revoke")]
  [Authorize(Policy = PolicyNames.RequirePersonalAccessTokenSelfWrite)]
  [ProducesResponseType<PersonalAccessTokenResponseDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
  public async Task<ActionResult<PersonalAccessTokenResponseDto>> Revoke(
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
      return Problem(
        detail: "User not found.",
        statusCode: StatusCodes.Status400BadRequest,
        title: V1ProblemTitles.InvalidRequest);
    }

    var result = await personalAccessTokenManager.Revoke(id, user.Id);
    if (!result.IsSuccess)
    {
      return Problem(
        detail: result.Reason,
        statusCode: StatusCodes.Status400BadRequest,
        title: V1ProblemTitles.InvalidRequest);
    }

    return Ok(ToV1ResponseDto(result.Value));
  }

  [HttpPut("{id:guid}")]
  [Authorize(Policy = PolicyNames.RequirePersonalAccessTokenSelfWrite)]
  [ProducesResponseType<PersonalAccessTokenResponseDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
  public async Task<ActionResult<PersonalAccessTokenResponseDto>> Update(
    [FromServices] IPersonalAccessTokenManager personalAccessTokenManager,
    [FromServices] UserManager<AppUser> userManager,
    [FromRoute] Guid id,
    [FromQuery] Guid tenantId,
    [FromBody] UpdatePersonalAccessTokenRequestDto request)
  {
    if (!User.TryResolveTenantId(tenantId, out _))
    {
      return Forbid();
    }

    var user = await userManager.GetUserAsync(User);
    if (user is null)
    {
      return Problem(
        detail: "User not found.",
        statusCode: StatusCodes.Status400BadRequest,
        title: V1ProblemTitles.InvalidRequest);
    }

    var result = await personalAccessTokenManager.Update(
      id,
      new InternalDtos.UpdatePersonalAccessTokenRequestDto(request.Name),
      user.Id);

    if (!result.IsSuccess)
    {
      return Problem(
        detail: result.Reason,
        statusCode: StatusCodes.Status400BadRequest,
        title: V1ProblemTitles.InvalidRequest);
    }

    return Ok(ToV1ResponseDto(result.Value));
  }

  private static PersonalAccessTokenResponseDto ToV1ResponseDto(
    InternalDtos.PersonalAccessTokenResponseDto token)
  {
    return new PersonalAccessTokenResponseDto(
      token.Id,
      token.Name,
      token.CreatedAt,
      token.LastUsed,
      token.PermissionCount,
      token.PermissionMode,
      token.ExpiresAt,
      token.RevokedAt);
  }
}
