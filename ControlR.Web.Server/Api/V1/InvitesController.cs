using Asp.Versioning;
using ControlR.Web.Server.Authz.Permissions;
using ControlR.Web.Server.Services.Authorization;
using Microsoft.AspNetCore.Mvc;
using InviteDtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.Invites;

namespace ControlR.Web.Server.Api.V1;

/// <summary>
/// Tenant invitation CRUD. The anonymous token-bearing accept flow stays on the internal
/// surface because its shape (no principal, activation code as the credential) cannot be a
/// V1 resource. Everything else follows the V1 conventions: required tenantId, 201 +
/// CreatedAtAction on create, 204 on delete, and an Items envelope on list. The activation
/// code embedded in <c>InviteUrl</c> is included only for callers holding TenantUsersWrite.
/// </summary>
[Route(HttpConstants.V1.InvitesEndpoint)]
[ApiController]
[Authorize]
[ApiVersion(ApiVersions.V1)]
public class InvitesController : ControllerBase
{
  [HttpPost]
  [Authorize(Policy = PolicyNames.RequireTenantUsersWrite)]
  [ProducesResponseType<InviteDtos.InviteResponseDto>(StatusCodes.Status201Created)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  [ProducesResponseType(StatusCodes.Status409Conflict)]
  public async Task<ActionResult<InviteDtos.InviteResponseDto>> Create(
    [FromServices] ITenantInvitesProvider tenantInvitesProvider,
    [FromQuery] Guid tenantId,
    [FromBody] InviteDtos.CreateInviteRequestDto request)
  {
    if (!User.TryResolveTenantId(tenantId, out var resolvedTenantId))
    {
      return Forbid();
    }

    var origin = Request.ToOrigin();
    var result = await tenantInvitesProvider.CreateInvite(
      request.InviteeEmail,
      resolvedTenantId,
      origin,
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
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
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
  [ProducesResponseType<InviteDtos.InvitesResponseDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  public async Task<ActionResult<InviteDtos.InvitesResponseDto>> GetAll(
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

    var origin = Request.ToOrigin();
    var invites = await tenantInvitesProvider.GetAllInvites(resolvedTenantId, origin, evalResult.Allowed);

    return Ok(new InviteDtos.InvitesResponseDto
    {
      Items = [.. invites.Select(ToV1Dto)]
    });
  }

  private static InviteDtos.InviteResponseDto ToV1Dto(InternalDtos.TenantInviteResponseDto invite)
  {
    return new InviteDtos.InviteResponseDto(
      invite.Id,
      invite.CreatedAt,
      invite.InviteeEmail,
      invite.InviteUrl);
  }
}
