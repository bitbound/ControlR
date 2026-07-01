using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Web.Server.Extensions;
using ControlR.Web.Server.Services.AgentInstaller;
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
    Guid? tenantId = null;
    Guid? creatorId = null;
    if (!User.IsServerPrincipal())
    {
      if (!User.TryGetTenantId(out var tid) ||
          !User.TryGetUserId(out var cid))
      {
        return BadRequest("User tenant or id not found.");
      }
      tenantId = tid;
      creatorId = cid;
    }

    if (!tenantId.HasValue || !creatorId.HasValue)
    {
      return BadRequest("Server service accounts cannot create installer keys.");
    }

    var dto = await _installerKeyManager.CreateKey(
        tenantId.Value,
        creatorId.Value,
        request.KeyType,
        request.AllowedUses,
        request.Expiration,
        request.FriendlyName);

    return Ok(dto);
  }

  [HttpDelete("{id:guid}")]
  public async Task<IActionResult> Delete([FromRoute] Guid id)
  {
    Guid? tenantId = null;
    Guid? userId = null;
    if (!User.IsServerPrincipal())
    {
      if (!User.TryGetTenantId(out var tid) || !User.TryGetUserId(out var uid))
      {
        return BadRequest("User tenant or id not found.");
      }
      tenantId = tid;
      userId = uid;
    }

    if (!tenantId.HasValue || !userId.HasValue)
    {
      return BadRequest("Server service accounts cannot delete installer keys.");
    }

    var isAdmin = User.IsInRole(RoleNames.TenantAdministrator);
    var result = await _installerKeyManager.DeleteKey(id, userId.Value, tenantId.Value, isAdmin);
    return result.ToActionResult();
  }

  [HttpGet]
  public async Task<ActionResult<IEnumerable<AgentInstallerKeyDto>>> GetAll()
  {
    Guid? tenantId = null;
    Guid? userId = null;
    if (!User.IsServerPrincipal())
    {
      if (!User.TryGetTenantId(out var tid) || !User.TryGetUserId(out var uid))
      {
        return BadRequest("User tenant or id not found.");
      }
      tenantId = tid;
      userId = uid;
    }

    if (!tenantId.HasValue || !userId.HasValue)
    {
      return BadRequest("Server service accounts cannot list installer keys.");
    }

    var isAdmin = User.IsInRole(RoleNames.TenantAdministrator);
    var keys = await _installerKeyManager.GetAllKeys(tenantId.Value, userId.Value, isAdmin);
    return keys.ToList();
  }

  [HttpGet("usages/{keyId:guid}")]
  public async Task<ActionResult<IReadOnlyList<AgentInstallerKeyUsageDto>>> GetUsages([FromRoute] Guid keyId)
  {
    Guid? tenantId = null;
    Guid? userId = null;
    if (!User.IsServerPrincipal())
    {
      if (!User.TryGetTenantId(out var tid) || !User.TryGetUserId(out var uid))
      {
        return BadRequest("User tenant or id not found.");
      }
      tenantId = tid;
      userId = uid;
    }

    if (!tenantId.HasValue || !userId.HasValue)
    {
      return BadRequest("Server service accounts cannot view installer key usages.");
    }

    var isAdmin = User.IsInRole(RoleNames.TenantAdministrator);
    var result = await _installerKeyManager.GetKeyUsages(keyId, userId.Value, tenantId.Value, isAdmin);
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
    Guid? tenantId = null;
    Guid? userId = null;
    if (!User.IsServerPrincipal())
    {
      if (!User.TryGetTenantId(out var tid) || !User.TryGetUserId(out var uid))
      {
        return BadRequest("User tenant or id not found.");
      }
      tenantId = tid;
      userId = uid;
    }

    if (!tenantId.HasValue || !userId.HasValue)
    {
      return BadRequest("Server service accounts cannot rename installer keys.");
    }

    var isAdmin = User.IsInRole(RoleNames.TenantAdministrator);
    var result = await _installerKeyManager.RenameKey(request.Id, request.FriendlyName, userId.Value, tenantId.Value, isAdmin);
    return result.ToActionResult();
  }
}
