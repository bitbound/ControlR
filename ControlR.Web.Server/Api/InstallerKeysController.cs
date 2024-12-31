using ControlR.Web.Client.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ControlR.Web.Server.Api;

[Route("api/installer-keys")]
[ApiController]
[Authorize]
public class InstallerKeysController : ControllerBase
{
  public async Task<ActionResult<CreateInstallerKeyResponseDto>> Create(
    [FromBody] CreateInstallerKeyRequestDto requestDto,
    [FromServices] TimeProvider timeProvider,
    [FromServices] IAgentInstallerKeyManager keyManager)
  {
    if (!User.TryGetTenantId(out var tenantId))
    {
      return Unauthorized();
    }

    if (!User.TryGetUserId(out var userId))
    {
      return Unauthorized();
    }

    if (requestDto.KeyType == InstallerKeyType.Unknown)
    {
      return BadRequest("Invalid key type.");
    }

    if (requestDto.KeyType == InstallerKeyType.TimeBased &&
       (!requestDto.Expiration.HasValue || requestDto.Expiration.Value < timeProvider.GetLocalNow()))
    {
      return BadRequest("Expiration date must be in the future.");
    }

    if (requestDto.KeyType == InstallerKeyType.UsageBased && requestDto.AllowedUses < 1)
    {
      return BadRequest("Allowed uses must be more than 0.");
    }

    var key = await keyManager.CreateKey(tenantId, userId, requestDto.KeyType, requestDto.AllowedUses, requestDto.Expiration);
    return new CreateInstallerKeyResponseDto(requestDto.KeyType, key.AccessToken, requestDto.AllowedUses, requestDto.Expiration);
  }
}
