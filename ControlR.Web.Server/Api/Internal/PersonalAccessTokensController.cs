using ControlR.Web.Server.Authz.Permissions;
using Microsoft.AspNetCore.Mvc;

namespace ControlR.Web.Server.Api.Internal;

[Route(HttpConstants.Internal.PersonalAccessTokensEndpoint)]
[ApiController]
[Authorize]
[EndpointGroupName(OpenApiConstants.InternalGroupName)]
public class PersonalAccessTokensController : ControllerBase
{

  [ApiDeprecated("/api/v1/personal-access-tokens?tenantId=", Note = "The replacement requires tenantId as a query parameter, returns 201, and answers delete with 204.")]
  [HttpPost]
  [Authorize(Policy = PolicyNames.RequirePersonalAccessTokenSelfWrite)]
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

    if (User.ToPrincipalDescriptor() is not { } actor)
    {
      return BadRequest("User ID not found.");
    }

    var result = await personalAccessTokenManager.CreateToken(request, user.Id, actor);
    if (!result.IsSuccess)
    {
      return BadRequest(result.Reason);
    }

    return Ok(result.Value);
  }

  [ApiDeprecated("/api/v1/personal-access-tokens/{id}?tenantId=", Note = "The replacement requires tenantId as a query parameter and answers with 204.")]
  [HttpDelete("{id}")]
  [Authorize(Policy = PolicyNames.RequirePersonalAccessTokenSelfWrite)]
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

  [ApiDeprecated("/api/v1/personal-access-tokens?tenantId=", Note = "The replacement requires tenantId as a query parameter and returns an Items envelope.")]
  [HttpGet]
  [Authorize(Policy = PolicyNames.RequirePersonalAccessTokenSelfRead)]
  public async Task<ActionResult<IEnumerable<InternalDtos.PersonalAccessTokenResponseDto>>> GetPersonalAccessTokens(
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

  [ApiDeprecated("/api/v1/personal-access-tokens/{id}?tenantId=", Note = "The replacement requires tenantId as a query parameter.")]
  [HttpPut("{id}")]
  [Authorize(Policy = PolicyNames.RequirePersonalAccessTokenSelfWrite)]
  public async Task<ActionResult<InternalDtos.PersonalAccessTokenResponseDto>> UpdatePersonalAccessToken(
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
