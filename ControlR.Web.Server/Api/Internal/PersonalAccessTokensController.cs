using ControlR.Libraries.Api.Contracts.Constants;
using Microsoft.AspNetCore.Mvc;

namespace ControlR.Web.Server.Api.Internal;

[Route(HttpConstants.Internal.PersonalAccessTokensEndpoint)]
[Route(HttpConstants.Legacy.PersonalAccessTokensEndpoint)]
[ApiController]
[Authorize]
[EndpointGroupName(OpenApiConstants.InternalGroupName)]
public class PersonalAccessTokensController : ControllerBase
{

  [HttpPost]
  public async Task<ActionResult<InternalDtos.CreatePersonalAccessTokenResponseDto>> CreatePersonalAccessToken(
    [FromServices] IPersonalAccessTokenManager personalAccessTokenManager,
    [FromServices] UserManager<AppUser> userManager,
    [FromBody] InternalDtos.CreatePersonalAccessTokenRequestDto request)
  {
    var user = await userManager.GetUserAsync(User);
    if (user is null || user.TenantId == Guid.Empty)
    {
      return BadRequest("User tenant not found");
    }

    var result = await personalAccessTokenManager.CreateToken(request, user.Id);
    if (!result.IsSuccess)
    {
      return BadRequest(result.Reason);
    }

    return Ok(result.Value);
  }

  [HttpDelete("{id}")]
  public async Task<ActionResult> DeletePersonalAccessToken(
    [FromServices] IPersonalAccessTokenManager personalAccessTokenManager,
    [FromServices] UserManager<AppUser> userManager,
    Guid id)
  {
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

    return Ok();
  }

  [HttpGet]
  public async Task<ActionResult<IEnumerable<InternalDtos.PersonalAccessTokenDto>>> GetPersonalAccessTokens(
    [FromServices] IPersonalAccessTokenManager personalAccessTokenManager,
    [FromServices] UserManager<AppUser> userManager)
  {
    var user = await userManager.GetUserAsync(User);
    if (user is null)
    {
      return BadRequest("User not found.");
    }

    var personalAccessTokens = await personalAccessTokenManager.GetForUser(user.Id);
    return Ok(personalAccessTokens);
  }

  [HttpPut("{id}")]
  public async Task<ActionResult<InternalDtos.PersonalAccessTokenDto>> UpdatePersonalAccessToken(
    [FromServices] IPersonalAccessTokenManager personalAccessTokenManager,
    [FromServices] UserManager<AppUser> userManager,
    Guid id,
    [FromBody] InternalDtos.UpdatePersonalAccessTokenRequestDto request)
  {
    var user = await userManager.GetUserAsync(User);
    if (user is null)
    {
      return BadRequest("User not found.");
    }

    var result = await personalAccessTokenManager.Update(id, request, user.Id);
    if (!result.IsSuccess)
    {
      return BadRequest(result.Reason);
    }

    return Ok(result.Value);
  }
}
