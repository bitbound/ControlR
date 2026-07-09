using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;
using Microsoft.AspNetCore.Mvc;

namespace ControlR.Web.Server.Api.Internal;

[Route(HttpConstants.Internal.InvitesEndpoint)]
[ApiController]
[Authorize(Policy = RequireUserPrincipalPolicy.PolicyName)]
public class InternalInvitesController : ControllerBase
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
}
