using Asp.Versioning;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Web.Server.Services.AgentInstaller;
using Microsoft.AspNetCore.Mvc;
using CreateInstallerKeyRequestDto = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V0.CreateInstallerKeyRequestDto;

namespace ControlR.Web.Server.Api.V0;

[Route(HttpConstants.V0.InstallerKeysEndpoint)]
[ApiController]
[Authorize(Policy = RequireServerServiceAccountPolicy.PolicyName)]
[ApiVersion(ApiVersions.V0)]
public class InstallerKeysController(IAgentInstallerKeyManager installerKeyManager) : ControllerBase
{
  private readonly IAgentInstallerKeyManager _installerKeyManager = installerKeyManager;

  [HttpPost]
  public async Task<ActionResult<V0Dtos.CreateInstallerKeyResponseDto>> Create(
      [FromBody] CreateInstallerKeyRequestDto request)
  {
    var internalDto = await _installerKeyManager.CreateKey(
        request.TenantId,
        request.CreatorId,
        request.CreatorKind,
        request.KeyType,
        request.AllowedUses,
        request.Expiration,
        request.FriendlyName);

    var dto = V0Dtos.CreateInstallerKeyResponseDto.From(internalDto);

    return Ok(dto);
  }
}
