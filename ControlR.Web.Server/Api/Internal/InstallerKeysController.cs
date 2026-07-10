using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Web.Server.Services.AgentInstaller;
using Microsoft.AspNetCore.Mvc;
using CreateInstallerKeyRequestDto = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal.CreateInstallerKeyRequestDto;

namespace ControlR.Web.Server.Api.Internal;

[Route(HttpConstants.Internal.InstallerKeysEndpoint)]
[ApiController]
[Authorize(Roles = $"{RoleNames.TenantAdministrator},{RoleNames.InstallerKeyManager}")]
[EndpointGroupName(OpenApiConstants.InternalGroupName)]
public class InstallerKeysController(IAgentInstallerKeyManager installerKeyManager) : ControllerBase
{
  private readonly IAgentInstallerKeyManager _installerKeyManager = installerKeyManager;

  [HttpPost]
  public async Task<ActionResult<InternalDtos.CreateInstallerKeyResponseDto>> Create(
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
        CreatorKind.User,
        request.KeyType,
        request.AllowedUses,
        request.Expiration,
        request.FriendlyName);

    return Ok(dto);
  }

  [HttpDelete("{id:guid}")]
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
  public async Task<ActionResult<IEnumerable<InternalDtos.AgentInstallerKeyDto>>> GetAll()
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
  public async Task<ActionResult<IReadOnlyList<InternalDtos.AgentInstallerKeyUsageDto>>> GetUsages([FromRoute] Guid keyId)
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

  [HttpPut("rename")]
  public async Task<IActionResult> Rename(
      [FromBody] InternalDtos.RenameInstallerKeyRequestDto request)
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
