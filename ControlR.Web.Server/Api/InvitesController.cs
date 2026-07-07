using ControlR.Web.Server.Extensions;
using Microsoft.AspNetCore.Mvc;
using ControlR.Libraries.Api.Contracts.Constants;

namespace ControlR.Web.Server.Api;

[Route(HttpConstants.InvitesEndpoint)]
[ApiController]
[Authorize(Roles = RoleNames.TenantAdministrator)]
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
  public async Task<ActionResult<TenantInviteResponseDto>> Create(
    [FromBody] TenantInviteRequestDto dto,
    [FromServices] ITenantInvitesProvider tenantInvitesProvider)
  {
    Guid? tenantId = null;
    if (!User.IsServerPrincipal())
    {
      if (!User.TryGetTenantId(out var tid))
        return Unauthorized();
      tenantId = tid;
    }

    if (!tenantId.HasValue)
    {
      return BadRequest("Server service accounts cannot create tenant invites.");
    }

    var origin = Request.ToOrigin();
    var result = await tenantInvitesProvider.CreateInvite(
      dto.InviteeEmail,
      tenantId.Value,
      origin,
      HttpContext.RequestAborted);

    return result.ToActionResult();
  }

  [HttpDelete("{inviteId:guid}")]
  public async Task<IActionResult> Delete(
    [FromRoute] Guid inviteId,
    [FromServices] ITenantInvitesProvider tenantInvitesProvider)
  {
    Guid? tenantId = null;
    if (!User.IsServerPrincipal())
    {
      if (!User.TryGetTenantId(out var tid))
        return Unauthorized();
      tenantId = tid;
    }

    if (!tenantId.HasValue)
    {
      return BadRequest("Server service accounts cannot delete tenant invites.");
    }

    var result = await tenantInvitesProvider.DeleteInvite(inviteId, tenantId.Value);

    return result.ToActionResult();
  }

  [HttpGet]
  public async Task<ActionResult<TenantInviteResponseDto[]>> GetAll(
    [FromServices] ITenantInvitesProvider tenantInvitesProvider)
  {
    Guid? tenantId = null;
    if (!User.IsServerPrincipal())
    {
      if (!User.TryGetTenantId(out var tid))
        return Unauthorized();
      tenantId = tid;
    }

    if (!tenantId.HasValue)
    {
      return BadRequest("Server service accounts cannot list tenant invites.");
    }

    var origin = Request.ToOrigin();
    return await tenantInvitesProvider.GetAllInvites(tenantId.Value, origin);
  }
}
