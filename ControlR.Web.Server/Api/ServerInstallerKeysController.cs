using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Web.Server.Authz.Policies;
using ControlR.Web.Server.Services.AgentInstaller;
using Microsoft.AspNetCore.Mvc;

namespace ControlR.Web.Server.Api;

[Route(HttpConstants.InstallerKeysEndpoint + "/server")]
[ApiController]
[Authorize(Policy = RequireServerServiceAccountPolicy.PolicyName)]
public class ServerInstallerKeysController(IAgentInstallerKeyManager installerKeyManager) : ControllerBase
{
  private readonly IAgentInstallerKeyManager _installerKeyManager = installerKeyManager;

  [HttpPost]
  public async Task<ActionResult<CreateInstallerKeyResponseDto>> Create(
    [FromBody] ServerCreateInstallerKeyRequestDto request)
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

  [HttpDelete("{id:guid}")]
  public async Task<IActionResult> Delete(
    [FromRoute] Guid id,
    [FromQuery] Guid tenantId,
    [FromQuery] Guid userId,
    [FromQuery] bool isTenantAdmin)
  {
    var result = await _installerKeyManager.DeleteKey(id, userId, tenantId, isTenantAdmin);
    return result.ToActionResult();
  }

  [HttpGet]
  public async Task<ActionResult<IEnumerable<AgentInstallerKeyDto>>> GetAll(
    [FromQuery] Guid tenantId,
    [FromQuery] Guid userId,
    [FromQuery] bool isTenantAdmin)
  {
    var keys = await _installerKeyManager.GetAllKeys(tenantId, userId, isTenantAdmin);
    return keys.ToList();
  }

  [HttpGet("usages/{keyId:guid}")]
  public async Task<ActionResult<IReadOnlyList<AgentInstallerKeyUsageDto>>> GetUsages(
    [FromRoute] Guid keyId,
    [FromQuery] Guid tenantId,
    [FromQuery] Guid userId,
    [FromQuery] bool isTenantAdmin)
  {
    var result = await _installerKeyManager.GetKeyUsages(keyId, userId, tenantId, isTenantAdmin);
    return result.ToActionResult();
  }

  [HttpPut("rename")]
  public async Task<IActionResult> Rename(
    [FromBody] ServerRenameInstallerKeyRequestDto request)
  {
    var result = await _installerKeyManager.RenameKey(
      request.Id,
      request.FriendlyName,
      request.UserId,
      request.TenantId,
      request.IsTenantAdmin);

    return result.ToActionResult();
  }
}