using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Web.Server.Services.AgentInstaller;
using Microsoft.AspNetCore.Mvc;
using CreateInstallerKeyRequestDto = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V0.CreateInstallerKeyRequestDto;

namespace ControlR.Web.Server.Api.V0;

[Route(HttpConstants.V0.InstallerKeysEndpoint)]
[ApiController]
[Authorize(Policy = RequireServerServiceAccountPolicy.PolicyName)]
public class InstallerKeysController(IAgentInstallerKeyManager installerKeyManager) : ControllerBase
{
  private readonly IAgentInstallerKeyManager _installerKeyManager = installerKeyManager;

  [HttpPost]
  public async Task<ActionResult<CreateInstallerKeyResponseDto>> Create(
      [FromBody] CreateInstallerKeyRequestDto request)
  {
    var dto = await _installerKeyManager.CreateKey(
        request.TenantId,
        request.CreatorId,
        request.CreatorKind,
        request.KeyType,
        request.AllowedUses,
        request.Expiration,
        request.FriendlyName);

    return Ok(dto);
  }
}
