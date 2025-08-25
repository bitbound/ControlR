using ControlR.Libraries.Shared.Constants;
using Microsoft.AspNetCore.Mvc;

namespace ControlR.Web.Server.Api;

[Route(HttpConstants.PersonalAccessTokensEndpoint)]
[ApiController]
[Authorize]
public class PersonalAccessTokensController(
  IPersonalAccessTokenManager personalAccessTokenManager,
  UserManager<AppUser> userManager) : ControllerBase
{
  private readonly IPersonalAccessTokenManager _personalAccessTokenManager = personalAccessTokenManager;
  private readonly UserManager<AppUser> _userManager = userManager;

  [HttpGet]
  public async Task<ActionResult<IEnumerable<PersonalAccessTokenDto>>> GetPersonalAccessTokens()
  {
    var user = await _userManager.GetUserAsync(User);
    if (user is null)
    {
      return BadRequest("User not found");
    }

    var personalAccessTokens = await _personalAccessTokenManager.GetForUser(user.Id);
    return Ok(personalAccessTokens);
  }

  [HttpPost]
  public async Task<ActionResult<CreatePersonalAccessTokenResponseDto>> CreatePersonalAccessToken([FromBody] CreatePersonalAccessTokenRequestDto request)
  {
    var user = await _userManager.GetUserAsync(User);
    if (user is null || user.TenantId == Guid.Empty)
    {
      return BadRequest("User tenant not found");
    }

    var result = await _personalAccessTokenManager.CreateToken(request, user.TenantId, user.Id);
    if (!result.IsSuccess)
    {
      return BadRequest(result.Reason);
    }

    return Ok(result.Value);
  }

  [HttpPut("{id}")]
  public async Task<ActionResult<PersonalAccessTokenDto>> UpdatePersonalAccessToken(Guid id, [FromBody] UpdatePersonalAccessTokenRequestDto request)
  {
    var user = await _userManager.GetUserAsync(User);
    if (user is null)
    {
      return BadRequest("User not found");
    }

    var result = await _personalAccessTokenManager.Update(id, request, user.Id);
    if (!result.IsSuccess)
    {
      return BadRequest(result.Reason);
    }

    return Ok(result.Value);
  }

  [HttpDelete("{id}")]
  public async Task<ActionResult> DeletePersonalAccessToken(Guid id)
  {
    var user = await _userManager.GetUserAsync(User);
    if (user is null)
    {
      return BadRequest("User not found");
    }

    var result = await _personalAccessTokenManager.Delete(id, user.Id);
    if (!result.IsSuccess)
    {
      return BadRequest(result.Reason);
    }

    return Ok();
  }
}
