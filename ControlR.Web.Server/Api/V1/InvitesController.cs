using Asp.Versioning;
using ControlR.Web.Server.Authz.Permissions;
using ControlR.Web.Server.Services.Authorization;
using Microsoft.AspNetCore.Mvc;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.Invites;

namespace ControlR.Web.Server.Api.V1;

/// <summary>
/// Tenant invitation CRUD. The anonymous token-bearing accept flow stays on the internal
/// surface.
/// </summary>
[Route(HttpConstants.V1.InvitesEndpoint)]
[ApiController]
[Authorize]
[ApiVersion(ApiVersions.V1)]
public class InvitesController : ControllerBase
{
  [HttpPost]
  [Authorize(Policy = PolicyNames.RequireTenantUsersWrite)]
  [ProducesResponseType<InviteResponseDto>(StatusCodes.Status201Created)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
  public async Task<ActionResult<InviteResponseDto>> Create(
    [FromServices] ITenantInvitesProvider tenantInvitesProvider,
    [FromQuery] Guid tenantId,
    [FromBody] CreateInviteRequestDto request)
  {
    if (!User.TryResolveTenantId(tenantId, out var resolvedTenantId))
    {
      return Forbid();
    }

    var result = await tenantInvitesProvider.CreateInvite(
      request.InviteeEmail,
      resolvedTenantId,
      HttpContext.RequestAborted);

    if (!result.IsSuccess)
    {
      return result.ToHttpResult().ToActionResult();
    }

    return CreatedAtAction(
      nameof(GetAll),
      new { tenantId = resolvedTenantId },
      ToV1Dto(result.Value));
  }

  [HttpDelete("{inviteId:guid}")]
  [Authorize(Policy = PolicyNames.RequireTenantUsersWrite)]
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
  public async Task<IActionResult> Delete(
    [FromServices] ITenantInvitesProvider tenantInvitesProvider,
    [FromRoute] Guid inviteId,
    [FromQuery] Guid tenantId)
  {
    if (!User.TryResolveTenantId(tenantId, out var resolvedTenantId))
    {
      return Forbid();
    }

    var result = await tenantInvitesProvider.DeleteInvite(inviteId, resolvedTenantId);

    return result.ToActionResult();
  }

  [HttpGet]
  [Authorize(Policy = PolicyNames.RequireUsersRead)]
  [ProducesResponseType<InvitesResponseDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
  public async Task<ActionResult<InvitesResponseDto>> GetAll(
    [FromServices] ITenantInvitesProvider tenantInvitesProvider,
    [FromServices] IPermissionEvaluator permissionEvaluator,
    [FromServices] IResourceDescriptorFactory resourceFactory,
    [FromQuery] Guid tenantId)
  {
    if (!User.TryResolveTenantId(tenantId, out var resolvedTenantId))
    {
      return Forbid();
    }

    // Only invite managers (TenantUsersWrite) may see the activation code. Read-only users
    // receive the invite metadata without the bearer secret. Evaluate the current credential
    // directly so a narrowed PAT cannot inherit the owning user's write permission.
    var callerPrincipal = User.ToPrincipalDescriptor();
    if (callerPrincipal is null)
    {
      return Unauthorized();
    }

    var resource = resourceFactory.CreateTenant(resolvedTenantId);
    var evalResult = await permissionEvaluator.Evaluate(
      callerPrincipal, PermissionNames.TenantUsersWrite, resource, HttpContext.RequestAborted);

    var result = await tenantInvitesProvider.GetAllInvites(resolvedTenantId, evalResult.Allowed);

    if (!result.IsSuccess)
    {
      return result.ToHttpResult().ToActionResult();
    }

    return Ok(new InvitesResponseDto
    {
      Items = [.. result.Value.Select(ToV1Dto)]
    });
  }

  private static InviteResponseDto ToV1Dto(InternalDtos.InviteResponseDto invite)
  {
    return new InviteResponseDto(
      invite.Id,
      invite.CreatedAt,
      invite.InviteeEmail,
      invite.InviteUrl);
  }
}
