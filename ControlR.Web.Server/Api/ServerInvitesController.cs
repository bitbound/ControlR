using ControlR.Libraries.Api.Contracts.Constants;
using Microsoft.AspNetCore.Mvc;

namespace ControlR.Web.Server.Api;

[Route(HttpConstants.InvitesEndpoint + "/server")]
[ApiController]
[Authorize(Policy = RequireServerServiceAccountPolicy.PolicyName)]
public class ServerInvitesController : ControllerBase
{
  [HttpPost]
  public async Task<ActionResult<TenantInviteResponseDto>> Create(
    [FromBody] ServerTenantInviteRequestDto dto,
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

  [HttpDelete("{tenantId:guid}/{inviteId:guid}")]
  public async Task<IActionResult> Delete(
    [FromRoute] Guid tenantId,
    [FromRoute] Guid inviteId,
    [FromServices] ITenantInvitesProvider tenantInvitesProvider)
  {
    var result = await tenantInvitesProvider.DeleteInvite(inviteId, tenantId);
    return result.ToActionResult();
  }

  [HttpGet("{tenantId:guid}")]
  public async Task<ActionResult<TenantInviteResponseDto[]>> GetAll(
    [FromRoute] Guid tenantId,
    [FromServices] ITenantInvitesProvider tenantInvitesProvider)
  {
    var origin = Request.ToOrigin();
    return await tenantInvitesProvider.GetAllInvites(tenantId, origin);
  }
}