using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Shared.Services.Encryption;
using ControlR.Web.Server.Services.AgentInstaller;
using ControlR.Web.Server.Services.DeviceManagement;
using Microsoft.AspNetCore.Mvc;
using CreateDeviceRequestDto = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal.CreateDeviceRequestDto;

namespace ControlR.Web.Server.Api;

[Route(HttpConstants.DevicesEndpoint)]
[ApiController]
[Authorize]
[EndpointGroupName("Legacy")]
public class DevicesController : ControllerBase
{

  [HttpPost]
  [AllowAnonymous]
  public async Task<ActionResult<DeviceResponseDto>> CreateDevice(
    [FromBody] CreateDeviceRequestDto requestDto,
    [FromServices] AppDb appDb,
    [FromServices] UserManager<AppUser> userManager,
    [FromServices] IAgentInstallerKeyManager keyManager,
    [FromServices] IDeviceManager deviceManager,
    [FromServices] IAgentVersionProvider agentVersionProvider,
    [FromServices] ILogger<DevicesController> logger,
    [FromServices] IEd25519KeyProvider keyProvider)
  {
    using var logScope = logger.BeginScope(requestDto);
    var deviceDto = requestDto.Device;

    if (deviceDto.Id == Guid.Empty)
    {
      logger.LogWarning("Invalid device ID.");
      return BadRequest();
    }

    // Validate public key format if provided.
    if (!string.IsNullOrWhiteSpace(requestDto.PublicKey))
    {
      var keyValidationResult = keyProvider.ValidatePublicKeyBase64(requestDto.PublicKey);
      if (!keyValidationResult.IsSuccess)
      {
        logger.LogWarning(
          "Public key validation failed for device {DeviceId}: {Reason}",
          deviceDto.Id,
          keyValidationResult.Reason);
        return BadRequest();
      }
    }

    // Validate key without consuming usage. We'll consume at the end if all checks pass.
    var keyResult = await keyManager.ValidateKey(requestDto.InstallerKeyId, requestDto.InstallerKeySecret);
    if (!keyResult.IsSuccess)
    {
      logger.LogWarning("Invalid installer key.");
      return BadRequest();
    }

    var installerKey = keyResult.Value;
    var tenantId = installerKey.TenantId;

    if (tenantId != deviceDto.TenantId)
    {
      logger.LogWarning("Installer key tenant does not match device tenant.");
      return BadRequest();
    }

    var existingDevice = await appDb.Devices.FirstOrDefaultAsync(x => x.Id == deviceDto.Id && x.TenantId == tenantId);
    if (existingDevice is not null)
    {
      logger.LogInformation("Device already exists.  Verifying user authorization.");

      if (installerKey.CreatorKind == CreatorKind.User)
      {
        var keyCreator = await userManager.FindByIdAsync($"{installerKey.CreatorId}");
        if (keyCreator is null)
        {
          logger.LogWarning("User not found.");
          return BadRequest();
        }

        var authResult = await deviceManager.CanInstallAgentOnDevice(keyCreator, existingDevice);

        if (!authResult)
        {
          logger.LogCritical("User is not authorized to install an agent on this device.");
          return Forbid();
        }
      }
    }

    // All checks passed - now consume the key usage
    var consumeResult = await keyManager.ValidateAndConsumeKey(
      requestDto.InstallerKeyId,
      requestDto.InstallerKeySecret,
      deviceDto.Id,
      HttpContext.Connection.RemoteIpAddress?.ToString());

    if (!consumeResult.IsSuccess)
    {
      logger.LogWarning("Failed to consume installer key usage.");
      return BadRequest();
    }

    var connectionContext = new DeviceConnectionContext(
      ConnectionId: string.Empty,
      RemoteIpAddress: HttpContext.Connection.RemoteIpAddress,
      LastSeen: DateTimeOffset.UtcNow,
      IsOnline: false);

    var entity = await deviceManager.AddOrUpdate(deviceDto, connectionContext, requestDto.TagIds, requestDto.PublicKey);

    var isOutdated = await GetIsOutdated(entity, agentVersionProvider);
    return entity.ToInternalResponseDto(isOutdated);
  }

  private static async Task<bool> GetIsOutdated(Device entity, IAgentVersionProvider agentVersionProvider)
  {
    var agentVersionResult = await agentVersionProvider.TryGetAgentVersion();
    if (!agentVersionResult.IsSuccess)
    {
      return false;
    }
    return entity.AgentVersion != agentVersionResult.Value.ToString();
  }
}
