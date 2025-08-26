using ControlR.Libraries.Shared.Constants;
using Microsoft.AspNetCore.Mvc;

namespace ControlR.Web.Server.Api;

[Route(HttpConstants.PersonalAccessTokensEndpoint)]
[ApiController]
[Authorize]
public class PersonalAccessTokensController : ControllerBase
{

  [HttpGet]
  public async Task<ActionResult<IEnumerable<PersonalAccessTokenDto>>> GetPersonalAccessTokens(
    [FromServices] IPersonalAccessTokenManager personalAccessTokenManager,
    [FromServices] UserManager<AppUser> userManager)
  {
    var user = await userManager.GetUserAsync(User);
    if (user is null)
    {
      return BadRequest("User not found");
    }

    var personalAccessTokens = await personalAccessTokenManager.GetForUser(user.Id);
    return Ok(personalAccessTokens);
  }

  [HttpPost]
  public async Task<ActionResult<CreatePersonalAccessTokenResponseDto>> CreatePersonalAccessToken(
    [FromServices] IPersonalAccessTokenManager personalAccessTokenManager,
    [FromServices] UserManager<AppUser> userManager,
    [FromBody] CreatePersonalAccessTokenRequestDto request)
  {
    var user = await userManager.GetUserAsync(User);
    if (user is null || user.TenantId == Guid.Empty)
    {
      return BadRequest("User tenant not found");
    }

    var result = await personalAccessTokenManager.CreateToken(request, user.TenantId, user.Id);
    if (!result.IsSuccess)
    {
      return BadRequest(result.Reason);
    }

    return Ok(result.Value);
  }

  [HttpPut("{id}")]
  public async Task<ActionResult<PersonalAccessTokenDto>> UpdatePersonalAccessToken(
    [FromServices] IPersonalAccessTokenManager personalAccessTokenManager,
    [FromServices] UserManager<AppUser> userManager,
    Guid id,
    [FromBody] UpdatePersonalAccessTokenRequestDto request)
  {
    var user = await userManager.GetUserAsync(User);
    if (user is null)
    {
      return BadRequest("User not found");
    }

    var result = await personalAccessTokenManager.Update(id, request, user.Id);
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
      return BadRequest("User not found");
    }

    var result = await personalAccessTokenManager.Delete(id, user.Id);
    if (!result.IsSuccess)
    {
      return BadRequest(result.Reason);
    }

    return Ok();
  }
}
