using ControlR.Web.Server.Authz.Permissions;
using ControlR.Web.Server.Services.AgentInstaller;
using ControlR.Web.Server.Services.Authorization;
using Microsoft.AspNetCore.Mvc;
using CreateInstallerKeyRequestDto = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal.CreateInstallerKeyRequestDto;

namespace ControlR.Web.Server.Api.Internal;

[Route(HttpConstants.Internal.InstallerKeysEndpoint)]
[ApiController]
[Authorize]
[EndpointGroupName(OpenApiConstants.InternalGroupName)]
public class InstallerKeysController(
  IAgentInstallerKeyManager installerKeyManager,
  IPermissionEvaluator permissionEvaluator,
  IResourceDescriptorFactory resourceFactory) : ControllerBase
{
  private readonly IAgentInstallerKeyManager _installerKeyManager = installerKeyManager;
  private readonly IPermissionEvaluator _permissionEvaluator = permissionEvaluator;
  private readonly IResourceDescriptorFactory _resourceFactory = resourceFactory;

  [HttpPost]
  [Authorize(Policy = PolicyNames.RequireInstallerKeyWrite)]
  [ApiDeprecated("/api/v1/installer-keys", Note = "The replacement requires TenantId in the request body.")]
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
        InstallerKeyCreatorKind.User,
        request.KeyType,
        request.AllowedUses,
        request.Expiration,
        request.FriendlyName);

    return Ok(dto);
  }

  [HttpDelete("{id:guid}")]
  [Authorize(Policy = PolicyNames.RequireInstallerKeyWrite)]
  [ApiDeprecated("/api/v1/installer-keys/{keyId}?tenantId=", Note = "The replacement requires tenantId as a query parameter.")]
  public async Task<IActionResult> Delete([FromRoute] Guid id)
  {
    if (!User.TryGetTenantId(out var tenantId) ||
        !User.TryGetUserId(out var userId))
    {
      return BadRequest("User tenant or id not found.");
    }

    var isAdmin = await CanManageAllKeys(HttpContext.RequestAborted);
    var result = await _installerKeyManager.DeleteKey(id, userId, tenantId, isAdmin);
    return result.ToActionResult();
  }

  [HttpGet]
  [Authorize(Policy = PolicyNames.RequireInstallerKeyRead)]
  [ApiDeprecated("/api/v1/installer-keys?tenantId=", Note = "The replacement requires tenantId as a query parameter and returns an Items envelope.")]
  public async Task<ActionResult<IEnumerable<InternalDtos.AgentInstallerKeyDto>>> GetAll()
  {
    if (!User.TryGetTenantId(out var tenantId) ||
        !User.TryGetUserId(out var userId))
    {
      return BadRequest("User tenant or id not found.");
    }

    var isAdmin = await CanManageAllKeys(HttpContext.RequestAborted);
    var keys = await _installerKeyManager.GetAllKeys(tenantId, userId, isAdmin);
    return keys.ToList();
  }

  [HttpGet("usages/{keyId:guid}")]
  [Authorize(Policy = PolicyNames.RequireInstallerKeyRead)]
  [ApiDeprecated("/api/v1/installer-keys/{keyId}/usages?tenantId=", Note = "The replacement requires tenantId as a query parameter and returns an Items envelope.")]
  public async Task<ActionResult<IReadOnlyList<InternalDtos.AgentInstallerKeyUsageDto>>> GetUsages([FromRoute] Guid keyId)
  {
    if (!User.TryGetTenantId(out var tenantId) ||
        !User.TryGetUserId(out var userId))
    {
      return BadRequest("User tenant or id not found.");
    }

    var isAdmin = await CanManageAllKeys(HttpContext.RequestAborted);
    var result = await _installerKeyManager.GetKeyUsages(keyId, userId, tenantId, isAdmin);
    return result.ToActionResult();
  }

  [HttpPut("rename")]
  [Authorize(Policy = PolicyNames.RequireInstallerKeyWrite)]
  [ApiDeprecated("/api/v1/installer-keys/{keyId}?tenantId=", Note = "The replacement takes the key id in the route, tenantId as a query parameter, a body with only friendlyName, and returns 204.")]
  public async Task<IActionResult> Rename(
      [FromBody] InternalDtos.RenameInstallerKeyRequestDto request)
  {
    if (!User.TryGetTenantId(out var tenantId) ||
        !User.TryGetUserId(out var userId))
    {
      return BadRequest("User tenant or id not found.");
    }

    var isAdmin = await CanManageAllKeys(HttpContext.RequestAborted);
    var result = await _installerKeyManager.RenameKey(request.Id, request.FriendlyName, userId, tenantId, isAdmin);
    return result.ToActionResult();
  }

  private async Task<bool> CanManageAllKeys(CancellationToken cancellationToken)
  {
    var principal = User.ToPrincipalDescriptor();
    if (principal is null)
    {
      return false;
    }

    if (!principal.TenantId.HasValue)
    {
      return false;
    }

    var result = await _permissionEvaluator.Evaluate(
      principal,
      PermissionNames.InstallerKeyManageAll,
      _resourceFactory.CreateTenant(principal.TenantId.Value),
      cancellationToken);
    return result.Allowed;
  }
}
