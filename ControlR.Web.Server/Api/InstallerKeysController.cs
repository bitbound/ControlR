using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Web.Server.Authz.Policies;
using ControlR.Web.Server.Services.AgentInstaller;
using Microsoft.AspNetCore.Mvc;

namespace ControlR.Web.Server.Api;

[Route(HttpConstants.InstallerKeysEndpoint)]
[ApiController]
[Authorize]
public class InstallerKeysController(IAgentInstallerKeyManager installerKeyManager) : ControllerBase
{
  private readonly IAgentInstallerKeyManager _installerKeyManager = installerKeyManager;

  /// <summary>
  /// Creates an installer key from the authenticated user's context.
  /// Tenant and creator are derived from claims.
  /// </summary>
  [HttpPost]
  [Authorize(Policy = RequireUserPrincipalPolicy.PolicyName)]
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
  [Authorize(Policy = RequireUserPrincipalPolicy.PolicyName)]
  public async Task<IActionResult> Delete([FromRoute] Guid id)
  {
    if (!User.TryGetTenantId(out var tenantId) ||
        !User.TryGetUserId(out var userId))
    {
      return BadRequest("User tenant or id not found.");
    }

    var isAdmin = User.IsInRole(RoleNames.TenantAdministrator);
    var result = await _installerKeyManager.DeleteKey(id, userId, tenantId, isAdmin);
    return result.ToActionResult();
  }

  [HttpGet]
  [Authorize(Policy = RequireUserPrincipalPolicy.PolicyName)]
  public async Task<ActionResult<IEnumerable<AgentInstallerKeyDto>>> GetAll()
  {
    if (!User.TryGetTenantId(out var tenantId) ||
        !User.TryGetUserId(out var userId))
    {
      return BadRequest("User tenant or id not found.");
    }

    var isAdmin = User.IsInRole(RoleNames.TenantAdministrator);
    var keys = await _installerKeyManager.GetAllKeys(tenantId, userId, isAdmin);
    return keys.ToList();
  }

  [HttpGet("usages/{keyId:guid}")]
  [Authorize(Policy = RequireUserPrincipalPolicy.PolicyName)]
  public async Task<ActionResult<IReadOnlyList<AgentInstallerKeyUsageDto>>> GetUsages([FromRoute] Guid keyId)
  {
    if (!User.TryGetTenantId(out var tenantId) ||
        !User.TryGetUserId(out var userId))
    {
      return BadRequest("User tenant or id not found.");
    }

    var isAdmin = User.IsInRole(RoleNames.TenantAdministrator);
    var result = await _installerKeyManager.GetKeyUsages(keyId, userId, tenantId, isAdmin);
    return result.ToActionResult();
  }

  [HttpPost("increment-usage/{keyId:guid}")]
  [AllowAnonymous]
  public async Task<IActionResult> IncrementUsage(
      [FromRoute] Guid keyId,
      [FromQuery] Guid? deviceId)
  {
    var remoteIp = HttpContext.Connection.RemoteIpAddress?.ToString();
    var result = await _installerKeyManager.IncrementUsage(keyId, deviceId, remoteIp);
    return result.ToActionResult();
  }

  /// <summary>
  /// Creates an installer key as a server principal.
  /// All fields including TenantId and CreatorId must be provided in the request.
  /// </summary>
  [HttpPost("issue")]
  [Authorize(Policy = RequireServerServiceAccountPolicy.PolicyName)]
  public async Task<ActionResult<CreateInstallerKeyResponseDto>> Issue(
      [FromBody] IssueInstallerKeyRequestDto request)
  {
    var dto = await _installerKeyManager.CreateKey(
        request.TenantId,
        request.CreatorId,
        request.KeyType,
        request.AllowedUses,
        request.Expiration,
        request.FriendlyName);

    return Ok(dto);
  }

  [HttpPut("rename")]
  [Authorize(Policy = RequireUserPrincipalPolicy.PolicyName)]
  public async Task<IActionResult> Rename(
      [FromBody] RenameInstallerKeyRequestDto request)
  {
    if (!User.TryGetTenantId(out var tenantId) ||
        !User.TryGetUserId(out var userId))
    {
      return BadRequest("User tenant or id not found.");
    }

    var isAdmin = User.IsInRole(RoleNames.TenantAdministrator);
    var result = await _installerKeyManager.RenameKey(request.Id, request.FriendlyName, userId, tenantId, isAdmin);
    return result.ToActionResult();
  }
}

