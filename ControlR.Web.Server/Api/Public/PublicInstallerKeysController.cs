using Microsoft.AspNetCore.Mvc;
using ControlR.Web.Server.Services.AgentInstaller;

namespace ControlR.Web.Server.Api.Public;

[Route("public/installer-keys")]
[ApiController]
[AllowAnonymous]
public class PublicInstallerKeysController(IAgentInstallerKeyManager installerKeyManager) : ControllerBase
{
  private readonly IAgentInstallerKeyManager _installerKeyManager = installerKeyManager;

  [HttpPost("increment-usage/{keyId:guid}")]
  public async Task<IActionResult> IncrementUsage(
      [FromRoute] Guid keyId,
      [FromQuery] Guid? deviceId)
  {
    var remoteIp = HttpContext.Connection.RemoteIpAddress?.ToString();
    var result = await _installerKeyManager.IncrementUsage(keyId, deviceId, remoteIp);
    return result.ToActionResult();
  }
}
