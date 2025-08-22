using ControlR.Libraries.Shared.Dtos.ServerApi;
using ControlR.Web.Client.Authz;
using ControlR.Web.Server.Data.Entities;
using ControlR.Web.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace ControlR.Web.Server.Api;

[Route("api/[controller]")]
[ApiController]
[Authorize(Roles = RoleNames.TenantAdministrator)]
public class ApiKeysController(
  IApiKeyManager apiKeyManager,
  UserManager<AppUser> userManager) : ControllerBase
{
  private readonly IApiKeyManager _apiKeyManager = apiKeyManager;
  private readonly UserManager<AppUser> _userManager = userManager;

  [HttpGet]
  public async Task<ActionResult<IEnumerable<ApiKeyDto>>> GetApiKeys()
  {
    var apiKeys = await _apiKeyManager.GetAll();
    return Ok(apiKeys);
  }

  [HttpPost]
  public async Task<ActionResult<CreateApiKeyResponseDto>> CreateApiKey([FromBody] CreateApiKeyRequestDto request)
  {
    var user = await _userManager.GetUserAsync(User);
    if (user is null || user.TenantId == Guid.Empty)
    {
      return BadRequest("User tenant not found");
    }

    var result = await _apiKeyManager.CreateWithKey(request, user.TenantId);
    if (!result.IsSuccess)
    {
      return BadRequest(result.Reason);
    }

    return Ok(result.Value);
  }

  [HttpPut("{id}")]
  public async Task<ActionResult<ApiKeyDto>> UpdateApiKey(Guid id, [FromBody] UpdateApiKeyRequestDto request)
  {
    var result = await _apiKeyManager.Update(id, request);
    if (!result.IsSuccess)
    {
      return BadRequest(result.Reason);
    }

    return Ok(result.Value);
  }

  [HttpDelete("{id}")]
  public async Task<ActionResult> DeleteApiKey(Guid id)
  {
    var result = await _apiKeyManager.Delete(id);
    if (!result.IsSuccess)
    {
      return BadRequest(result.Reason);
    }

    return Ok();
  }
}
