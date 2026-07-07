using ControlR.Web.Server.Extensions;
using Microsoft.AspNetCore.Mvc;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Web.Server.Authz.Policies;

namespace ControlR.Web.Server.Api;

[Route(HttpConstants.InvitesEndpoint)]
[ApiController]
[Authorize]
public class InvitesController : ControllerBase
{
  [HttpPost("accept")]
  [AllowAnonymous]
  public async Task<ActionResult<AcceptInvitationResponseDto>> AcceptInvite(
    [FromBody] AcceptInvitationRequestDto dto,
    [FromServices] ITenantInvitesProvider tenantInvitesProvider)
  {
    var result = await tenantInvitesProvider.AcceptInvite(dto);

    if (!result.IsSuccess)
    {
      return new AcceptInvitationResponseDto(false, result.Reason);
    }

    return result.Value;
  }

  [HttpPost]
  [Authorize(Policy = CombinedAuthorizationPolicies.RequireServerOrTenantAdminPolicy)]
  public async Task<ActionResult<TenantInviteResponseDto>> Create(
    [FromBody] TenantInviteRequestDto dto,
    [FromServices] ITenantInvitesProvider tenantInvitesProvider)
  {
    Guid tenantId;

    if (User.IsServerPrincipal())
    {
      if (!dto.TenantId.HasValue || dto.TenantId == Guid.Empty)
      {
        return BadRequest("TenantId is required.");
      }

      tenantId = dto.TenantId.Value;
    }
    else
    {
      if (!User.TryGetTenantId(out var tid))
      {
        return Unauthorized();
      }

      if (dto.TenantId.HasValue && dto.TenantId.Value != tid)
      {
        return Forbid();
      }

      tenantId = tid;
    }

    var origin = Request.ToOrigin();
    var result = await tenantInvitesProvider.CreateInvite(
      dto.InviteeEmail,
      tenantId,
      origin,
      HttpContext.RequestAborted);

    return result.ToActionResult();
  }

  [HttpDelete("{inviteId:guid}")]
  [Authorize(Policy = CombinedAuthorizationPolicies.RequireServerOrTenantAdminPolicy)]
  public async Task<IActionResult> Delete(
    [FromRoute] Guid inviteId,
    [FromQuery] Guid? tenantId,
    [FromServices] ITenantInvitesProvider tenantInvitesProvider)
  {
    Guid effectiveTenantId;

    if (User.IsServerPrincipal())
    {
      if (!tenantId.HasValue || tenantId == Guid.Empty)
      {
        return BadRequest("TenantId is required.");
      }

      effectiveTenantId = tenantId.Value;
    }
    else
    {
      if (!User.TryGetTenantId(out var tid))
      {
        return Unauthorized();
      }

      if (tenantId.HasValue && tenantId.Value != tid)
      {
        return Forbid();
      }

      effectiveTenantId = tid;
    }

    var result = await tenantInvitesProvider.DeleteInvite(inviteId, effectiveTenantId);

    return result.ToActionResult();
  }

  [HttpGet]
  [Authorize(Policy = CombinedAuthorizationPolicies.RequireServerOrTenantAdminPolicy)]
  public async Task<ActionResult<TenantInviteResponseDto[]>> GetAll(
    [FromQuery] Guid? tenantId,
    [FromServices] ITenantInvitesProvider tenantInvitesProvider)
  {
    Guid effectiveTenantId;

    if (User.IsServerPrincipal())
    {
      if (!tenantId.HasValue || tenantId == Guid.Empty)
      {
        return BadRequest("TenantId is required.");
      }

      effectiveTenantId = tenantId.Value;
    }
    else
    {
      if (!User.TryGetTenantId(out var tid))
      {
        return Unauthorized();
      }

      if (tenantId.HasValue && tenantId.Value != tid)
      {
        return Forbid();
      }

      effectiveTenantId = tid;
    }

    var origin = Request.ToOrigin();
    return await tenantInvitesProvider.GetAllInvites(effectiveTenantId, origin);
  }
}
