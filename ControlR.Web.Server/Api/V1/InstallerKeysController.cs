using Asp.Versioning;
using ControlR.Web.Server.Services.AgentInstaller;
using Microsoft.AspNetCore.Mvc;
using CreateInstallerKeyRequestDto = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.CreateInstallerKeyRequestDto;

namespace ControlR.Web.Server.Api.V1;

[Route(HttpConstants.V1.InstallerKeysEndpoint)]
[ApiController]
[Authorize(Policy = PolicyNames.RequireInstallerKeyWrite)]
[ApiVersion(ApiVersions.V1)]
public class InstallerKeysController(IAgentInstallerKeyManager installerKeyManager) : ControllerBase
{
  private readonly IAgentInstallerKeyManager _installerKeyManager = installerKeyManager;

  [HttpPost]
  public async Task<ActionResult<V1Dtos.CreateInstallerKeyResponseDto>> Create(
      [FromBody] CreateInstallerKeyRequestDto request)
  {
    if (!User.TryGetPrincipalId(out var creatorId))
    {
      return Forbid();
    }

    if (!User.TryResolveTenantId(request.TenantId, out var tenantId))
    {
      return Forbid();
    }

    var internalDto = await _installerKeyManager.CreateKey(
        tenantId,
        creatorId,
        User.GetCreatorKind(),
        request.KeyType,
        request.AllowedUses,
        request.Expiration,
        request.FriendlyName);

    var dto = V1Dtos.CreateInstallerKeyResponseDto.From(internalDto);

    return Ok(dto);
  }
}
