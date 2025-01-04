using ControlR.Web.Client.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ControlR.Web.Server.Api;

[Route("api/installer-keys")]
[ApiController]
[Authorize(Roles = RoleNames.AgentInstaller)]
public class InstallerKeysController : ControllerBase
{
  public async Task<ActionResult<CreateInstallerKeyResponseDto>> Create(
    [FromBody] CreateInstallerKeyRequestDto requestDto,
    [FromServices] TimeProvider timeProvider,
    [FromServices] IAgentInstallerKeyManager keyManager,
    [FromServices] ILogger<InstallerKeysController> logger)
  {
    if (!User.TryGetTenantId(out var tenantId))
    {
      logger.LogWarning("Failed to get tenant ID.  Request DTO: {@Dto}", requestDto);
      return Unauthorized();
    }

    if (!User.TryGetUserId(out var userId))
    {
      logger.LogWarning("Failed to get user ID.  Request DTO: {@Dto}", requestDto);
      return Unauthorized();
    }

    if (requestDto.KeyType == InstallerKeyType.Unknown)
    {
      logger.LogWarning("Invalid key type.  Request DTO: {@Dto}", requestDto);
      return BadRequest("Invalid key type.");
    }

    if (requestDto.KeyType == InstallerKeyType.TimeBased &&
       (!requestDto.Expiration.HasValue || requestDto.Expiration.Value < timeProvider.GetLocalNow()))
    {
      logger.LogWarning("Invalid expiration date.  Request DTO: {@Dto}", requestDto);
      return BadRequest("Expiration date must be in the future.");
    }

    if (requestDto.KeyType == InstallerKeyType.UsageBased && requestDto.AllowedUses < 1)
    {
      logger.LogWarning("No more uses allowed on the key.  Request DTO: {@Dto}", requestDto);
      return BadRequest("Allowed uses must be more than 0.");
    }

    logger.LogInformation("Installer key created.  Request DTO: {@Dto}", requestDto);
    var key = await keyManager.CreateKey(tenantId, userId, requestDto.KeyType, requestDto.AllowedUses, requestDto.Expiration);
    return new CreateInstallerKeyResponseDto(requestDto.KeyType, key.KeySecret, requestDto.AllowedUses, requestDto.Expiration);
  }
}
