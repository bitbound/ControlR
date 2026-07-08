using ControlR.Libraries.Api.Contracts.Constants;
using Microsoft.AspNetCore.Mvc;

namespace ControlR.Web.Server.Api.Public;

[Route(HttpConstants.Public.InvitesEndpoint)]
[ApiController]
[AllowAnonymous]
public class PublicInvitesController : ControllerBase
{
  [HttpPost("accept")]
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
}
