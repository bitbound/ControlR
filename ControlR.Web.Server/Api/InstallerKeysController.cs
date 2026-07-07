using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Web.Server.Authz.Policies;
using ControlR.Web.Server.Extensions;
using ControlR.Web.Server.Services.AgentInstaller;
using Microsoft.AspNetCore.Mvc;

namespace ControlR.Web.Server.Api;

[Route(HttpConstants.InstallerKeysEndpoint)]
[ApiController]
[Authorize(Policy = CombinedAuthorizationPolicies.RequireServerOrTenantAdminOrInstallerKeyManagerPolicy)]
public class InstallerKeysController(IAgentInstallerKeyManager installerKeyManager) : ControllerBase
{
  private readonly IAgentInstallerKeyManager _installerKeyManager = installerKeyManager;

  [HttpPost]
  public async Task<ActionResult<CreateInstallerKeyResponseDto>> Create(
      [FromBody] CreateInstallerKeyRequestDto request)
  {
    Guid tenantId;
    Guid creatorId;

    if (User.IsServerPrincipal())
    {
      if (!request.TenantId.HasValue || request.TenantId == Guid.Empty)
      {
        return BadRequest("TenantId is required.");
      }

      if (!request.CreatorId.HasValue || request.CreatorId == Guid.Empty)
      {
        return BadRequest("CreatorId is required.");
      }

      tenantId = request.TenantId.Value;
      creatorId = request.CreatorId.Value;
    }
    else
    {
      if (!User.TryGetTenantId(out var tid) ||
          !User.TryGetUserId(out var cid))
      {
        return BadRequest("User tenant or id not found.");
      }

      if (request.TenantId.HasValue && request.TenantId.Value != tid)
      {
        return Forbid();
      }

      if (request.CreatorId.HasValue && request.CreatorId.Value != cid)
      {
        return Forbid();
      }

      tenantId = tid;
      creatorId = cid;
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
    [FromQuery] Guid? tenantId = null,
    [FromQuery] Guid? userId = null,
    [FromQuery] bool? isTenantAdmin = null)
  {
    var effectiveIsTenantAdmin = User.IsServerPrincipal() ? isTenantAdmin ?? true : User.IsInRole(RoleNames.TenantAdministrator);
    var effectiveUserId = userId ?? Guid.Empty;
    var effectiveTenantId = tenantId;

    if (!User.IsServerPrincipal())
    {
      if (!User.TryGetTenantId(out var tid) || !User.TryGetUserId(out var uid))
      {
        return BadRequest("User tenant or id not found.");
      }

      if (tenantId.HasValue && tenantId.Value != tid)
      {
        return Forbid();
      }

      if (userId.HasValue && userId.Value != uid)
      {
        return Forbid();
      }

      if (isTenantAdmin.HasValue && isTenantAdmin.Value != User.IsInRole(RoleNames.TenantAdministrator))
      {
        return Forbid();
      }

      effectiveTenantId = tid;
      effectiveUserId = uid;
    }

    if (!effectiveTenantId.HasValue)
    {
      return BadRequest("TenantId is required.");
    }

    if (!effectiveIsTenantAdmin && effectiveUserId == Guid.Empty)
    {
      return BadRequest("UserId is required when IsTenantAdmin is false.");
    }

    var result = await _installerKeyManager.DeleteKey(id, effectiveUserId, effectiveTenantId.Value, effectiveIsTenantAdmin);
    return result.ToActionResult();
  }

  [HttpGet]
  public async Task<ActionResult<IEnumerable<AgentInstallerKeyDto>>> GetAll(
    [FromQuery] Guid? tenantId = null,
    [FromQuery] Guid? userId = null,
    [FromQuery] bool? isTenantAdmin = null)
  {
    var effectiveIsTenantAdmin = User.IsServerPrincipal() ? isTenantAdmin ?? true : User.IsInRole(RoleNames.TenantAdministrator);
    var effectiveUserId = userId ?? Guid.Empty;
    var effectiveTenantId = tenantId;

    if (!User.IsServerPrincipal())
    {
      if (!User.TryGetTenantId(out var tid) || !User.TryGetUserId(out var uid))
      {
        return BadRequest("User tenant or id not found.");
      }

      if (tenantId.HasValue && tenantId.Value != tid)
      {
        return Forbid();
      }

      if (userId.HasValue && userId.Value != uid)
      {
        return Forbid();
      }

      if (isTenantAdmin.HasValue && isTenantAdmin.Value != User.IsInRole(RoleNames.TenantAdministrator))
      {
        return Forbid();
      }

      effectiveTenantId = tid;
      effectiveUserId = uid;
    }

    if (!effectiveTenantId.HasValue)
    {
      return BadRequest("TenantId is required.");
    }

    if (!effectiveIsTenantAdmin && effectiveUserId == Guid.Empty)
    {
      return BadRequest("UserId is required when IsTenantAdmin is false.");
    }

    var keys = await _installerKeyManager.GetAllKeys(effectiveTenantId.Value, effectiveUserId, effectiveIsTenantAdmin);
    return keys.ToList();
  }

  [HttpGet("usages/{keyId:guid}")]
  public async Task<ActionResult<IReadOnlyList<AgentInstallerKeyUsageDto>>> GetUsages(
    [FromRoute] Guid keyId,
    [FromQuery] Guid? tenantId = null,
    [FromQuery] Guid? userId = null,
    [FromQuery] bool? isTenantAdmin = null)
  {
    var effectiveIsTenantAdmin = User.IsServerPrincipal() ? isTenantAdmin ?? true : User.IsInRole(RoleNames.TenantAdministrator);
    var effectiveUserId = userId ?? Guid.Empty;
    var effectiveTenantId = tenantId;

    if (!User.IsServerPrincipal())
    {
      if (!User.TryGetTenantId(out var tid) || !User.TryGetUserId(out var uid))
      {
        return BadRequest("User tenant or id not found.");
      }

      if (tenantId.HasValue && tenantId.Value != tid)
      {
        return Forbid();
      }

      if (userId.HasValue && userId.Value != uid)
      {
        return Forbid();
      }

      if (isTenantAdmin.HasValue && isTenantAdmin.Value != User.IsInRole(RoleNames.TenantAdministrator))
      {
        return Forbid();
      }

      effectiveTenantId = tid;
      effectiveUserId = uid;
    }

    if (!effectiveTenantId.HasValue)
    {
      return BadRequest("TenantId is required.");
    }

    if (!effectiveIsTenantAdmin && effectiveUserId == Guid.Empty)
    {
      return BadRequest("UserId is required when IsTenantAdmin is false.");
    }

    var result = await _installerKeyManager.GetKeyUsages(keyId, effectiveUserId, effectiveTenantId.Value, effectiveIsTenantAdmin);
    return result.ToActionResult();
  }

  [HttpPost("increment-usage/{keyId:guid}")]
  public async Task<IActionResult> IncrementUsage(
      [FromRoute] Guid keyId,
      [FromQuery] Guid? deviceId)
  {
    var remoteIp = HttpContext.Connection.RemoteIpAddress?.ToString();
    var result = await _installerKeyManager.IncrementUsage(keyId, deviceId, remoteIp);
    return result.ToActionResult();
  }

  [HttpPut("rename")]
  public async Task<IActionResult> Rename(
      [FromBody] RenameInstallerKeyRequestDto request)
  {
    var effectiveIsTenantAdmin = User.IsServerPrincipal() ? request.IsTenantAdmin ?? true : User.IsInRole(RoleNames.TenantAdministrator);
    var effectiveUserId = request.UserId ?? Guid.Empty;
    var effectiveTenantId = request.TenantId;

    if (!User.IsServerPrincipal())
    {
      if (!User.TryGetTenantId(out var tid) || !User.TryGetUserId(out var uid))
      {
        return BadRequest("User tenant or id not found.");
      }

      if (request.TenantId.HasValue && request.TenantId.Value != tid)
      {
        return Forbid();
      }

      if (request.UserId.HasValue && request.UserId.Value != uid)
      {
        return Forbid();
      }

      if (request.IsTenantAdmin.HasValue && request.IsTenantAdmin.Value != User.IsInRole(RoleNames.TenantAdministrator))
      {
        return Forbid();
      }

      effectiveTenantId = tid;
      effectiveUserId = uid;
    }

    if (!effectiveTenantId.HasValue)
    {
      return BadRequest("TenantId is required.");
    }

    if (!effectiveIsTenantAdmin && effectiveUserId == Guid.Empty)
    {
      return BadRequest("UserId is required when IsTenantAdmin is false.");
    }

    var result = await _installerKeyManager.RenameKey(request.Id, request.FriendlyName, effectiveUserId, effectiveTenantId.Value, effectiveIsTenantAdmin);
    return result.ToActionResult();
  }
}
