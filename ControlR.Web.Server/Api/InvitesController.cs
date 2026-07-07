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
  [Authorize(Policy = RequireUserPrincipalPolicy.PolicyName)]
  public async Task<ActionResult<TenantInviteResponseDto>> Create(
    [FromBody] TenantInviteRequestDto dto,
    [FromServices] ITenantInvitesProvider tenantInvitesProvider)
  {
    if (!User.TryGetTenantId(out var tenantId))
    {
      return Unauthorized();
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
  [Authorize(Policy = RequireUserPrincipalPolicy.PolicyName)]
  public async Task<IActionResult> Delete(
    [FromRoute] Guid inviteId,
    [FromServices] ITenantInvitesProvider tenantInvitesProvider)
  {
    if (!User.TryGetTenantId(out var tenantId))
    {
      return Unauthorized();
    }

    var result = await tenantInvitesProvider.DeleteInvite(inviteId, tenantId);

    return result.ToActionResult();
  }

  [HttpGet]
  [Authorize(Policy = RequireUserPrincipalPolicy.PolicyName)]
  public async Task<ActionResult<TenantInviteResponseDto[]>> GetAll(
    [FromServices] ITenantInvitesProvider tenantInvitesProvider)
  {
    if (!User.TryGetTenantId(out var tenantId))
    {
      return Unauthorized();
    }

    var origin = Request.ToOrigin();
    return await tenantInvitesProvider.GetAllInvites(tenantId, origin);
  }

  [HttpPost("issue")]
  [Authorize(Policy = RequireServerServiceAccountPolicy.PolicyName)]
  public async Task<ActionResult<TenantInviteResponseDto>> Issue(
    [FromBody] IssueTenantInviteRequestDto dto,
    [FromServices] ITenantInvitesProvider tenantInvitesProvider)
  {
    var origin = Request.ToOrigin();
    var result = await tenantInvitesProvider.CreateInvite(
      dto.InviteeEmail,
      dto.TenantId,
      origin,
      HttpContext.RequestAborted);

    return result.ToActionResult();
  }
}
