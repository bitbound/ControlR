using ControlR.Libraries.Shared.Constants;
using Microsoft.AspNetCore.Mvc;

namespace ControlR.Web.Server.Api;

[Route(HttpConstants.InstallerKeysEndpoint)]
[ApiController]
[Authorize(Roles = $"{RoleNames.TenantAdministrator},{RoleNames.InstallerKeyManager}")]
public class InstallerKeysController(IAgentInstallerKeyManager installerKeyManager) : ControllerBase
{
  private readonly IAgentInstallerKeyManager _installerKeyManager = installerKeyManager;

  [HttpPost]
  public async Task<ActionResult<CreateInstallerKeyResponseDto>> Create(
      [FromBody] CreateInstallerKeyRequestDto request)
  {
    if (!User.TryGetTenantId(out var tenantId) ||
        !User.TryGetUserId(out var creatorId))
    {
      return BadRequest("User tenant or id not found.");
    }

    var dto = await _installerKeyManager.CreateKey(
        tenantId,
        creatorId,
        request.KeyType,
        request.AllowedUses,
        request.Expiration,
        request.FriendlyName);

    return Ok(dto);
  }
  [HttpDelete("{id:guid}")]
  public async Task<IActionResult> Delete(
      [FromRoute] Guid id,
      [FromServices] AppDb db,
      [FromServices] ILogger<InstallerKeysController> logger)
  {
    if (!User.TryGetUserId(out var userId))
    {
      return BadRequest("User id not found.");
    }

    var key = await db.AgentInstallerKeys.FindAsync(id);

    if (key is null)
    {
      return NotFound();
    }

    if (!User.IsInRole(RoleNames.TenantAdministrator) && key.CreatorId != userId)
    {
      logger.LogWarning("User {UserId} attempted to delete installer key {KeyId} without permission.", userId, id);
      return Forbid();
    }

    db.AgentInstallerKeys.Remove(key);
    await db.SaveChangesAsync();

    return NoContent();
  }

  [HttpGet]
  public async Task<ActionResult<IEnumerable<AgentInstallerKeyDto>>> GetAll(
      [FromServices] AppDb db,
      [FromServices] ILogger<InstallerKeysController> logger)
  {
    if (!User.TryGetTenantId(out var tenantId))
    {
      return BadRequest("User tenant not found.");
    }

    if (!User.TryGetUserId(out var userId))
    {
      logger.LogWarning("User id not found when attempting to get all installer keys.");
      return BadRequest("User id not found.");
    }

    var query = db
      .AgentInstallerKeys
      .Where(x => x.TenantId == tenantId);

    if (!User.IsInRole(RoleNames.TenantAdministrator))
    {
      query = query.Where(x => x.CreatorId == userId);
    }

    var keys = await query.Include(x => x.Usages).ToListAsync();
    return keys.Select(x => x.ToDto()).ToList();
  }

  [HttpGet("usages/{keyId:guid}")]
  public async Task<ActionResult<IEnumerable<AgentInstallerKeyUsageDto>>> GetUsages(
      [FromRoute] Guid keyId,
      [FromServices] AppDb db,
      [FromServices] ILogger<InstallerKeysController> logger)
  {
    if (!User.TryGetTenantId(out var tenantId))
    {
      return BadRequest("User tenant not found.");
    }
    if (!User.TryGetUserId(out var userId))
    {
      return BadRequest("User id not found.");
    }

    var key = await db.AgentInstallerKeys
        .Include(x => x.Usages)
        .FirstOrDefaultAsync(x => x.Id == keyId);

    if (key is null)
    {
      return NotFound();
    }

    if (!User.IsInRole(RoleNames.TenantAdministrator) && userId != key.CreatorId)
    {
      logger.LogWarning("User {UserId} attempted to access usages of installer key {KeyId} without permission.", userId, keyId);
      return Forbid();
    }

    return key.Usages
        .Select(x => new AgentInstallerKeyUsageDto(x.Id, x.DeviceId, x.CreatedAt, x.RemoteIpAddress))
        .ToList();
  }

  [HttpPost("increment-usage/{keyId:guid}")]
  public async Task<IActionResult> IncrementUsage(
      [FromRoute] Guid keyId,
      [FromQuery] Guid? deviceId,
      [FromServices] ILogger<InstallerKeysController> logger)
  {
    var remoteIp = HttpContext.Connection.RemoteIpAddress?.ToString();
    var result = await _installerKeyManager.IncrementUsage(keyId, deviceId, remoteIp);

    if (!result.IsSuccess)
    {
      logger.LogWarning("Failed to increment usage for key {KeyId}: {Reason}", keyId, result.Reason);
      return BadRequest(result.Reason);
    }

    return Ok();
  }

  [HttpPut("rename")]
  public async Task<IActionResult> Rename(
      [FromBody] RenameInstallerKeyRequestDto request,
      [FromServices] AppDb db,
      [FromServices] ILogger<InstallerKeysController> logger)
  {
    if (!User.TryGetUserId(out var userId))
    {
      return BadRequest("User id not found.");
    }

    var key = await db.AgentInstallerKeys.FindAsync(request.Id);

    if (key is null)
    {
      return NotFound();
    }

    if (!User.IsInRole(RoleNames.TenantAdministrator) && userId != key.CreatorId)
    {
      logger.LogWarning("User {UserId} attempted to rename installer key {KeyId} without permission.", userId, request.Id);
      return Forbid();
    }

    key.FriendlyName = request.FriendlyName;
    await db.SaveChangesAsync();

    return Ok();
  }
}