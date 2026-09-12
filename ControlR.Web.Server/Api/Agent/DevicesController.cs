using ControlR.Libraries.Shared.Services.Encryption;
using ControlR.Web.Server.Extensions.Dtos.Internal;
using ControlR.Web.Server.Services.AgentInstaller;
using ControlR.Web.Server.Services.Authorization.Capabilities;
using ControlR.Web.Server.Services.DeviceManagement;
using Microsoft.AspNetCore.Mvc;

namespace ControlR.Web.Server.Api.Agent;

[Route(HttpConstants.Agent.DevicesEndpoint)]
[Route(HttpConstants.Agent.LegacyDevicesEndpoint)]
[ApiController]
[AllowAnonymous]
[EndpointGroupName(OpenApiConstants.InternalGroupName)]
public class DevicesController : ControllerBase
{
  [HttpPost]
  public async Task<ActionResult<InternalDtos.DeviceResponseDto>> CreateDevice(
    [FromBody] InternalDtos.CreateDeviceRequestDto requestDto,
    [FromServices] AppDb appDb,
    [FromServices] UserManager<AppUser> userManager,
    [FromServices] IAgentInstallerKeyManager keyManager,
    [FromServices] IDeviceManager deviceManager,
    [FromServices] IAgentVersionProvider agentVersionProvider,
    [FromServices] ILogger<DevicesController> logger,
    [FromServices] IEd25519KeyProvider keyProvider,
    [FromServices] IDeviceAuthorizationService deviceAuthorizationService)
  {
    using var logScope = logger.BeginScope(requestDto);
    var deviceDto = requestDto.Device;

    if (deviceDto.Id == Guid.Empty)
    {
      logger.LogWarning("Invalid device ID.");
      return BadRequest();
    }

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

    // Tracks whether the server-SA check below already passed, so the tag-authorization switch
    // does not repeat the query for a ServerServiceAccount-keyed re-provision request.
    var serverSaValidated = false;
    if (existingDevice is not null)
    {
      logger.LogInformation("Device already exists.  Verifying user authorization.");

      switch (installerKey.CreatorKind)
      {
        case InstallerKeyCreatorKind.User:
          var keyCreator = await userManager.FindByIdAsync($"{installerKey.CreatorId}");
          if (keyCreator is null)
          {
            logger.LogWarning("User not found.");
            return BadRequest();
          }

          var authResult = await deviceAuthorizationService.CanInstallAgentOnDevice(keyCreator, existingDevice);

          if (!authResult)
          {
            logger.LogCritical("User is not authorized to install an agent on this device.");
            return Forbid();
          }
          break;

        case InstallerKeyCreatorKind.TenantServiceAccount:
          var serviceAccount = await appDb.ServiceAccounts
            .FirstOrDefaultAsync(x => x.Id == installerKey.CreatorId && x.TenantId == tenantId && x.IsEnabled);
          if (serviceAccount is null)
          {
            logger.LogWarning("Service account not found or disabled.");
            return BadRequest();
          }

          var accountAuthResult = await deviceAuthorizationService.CanInstallAgentOnDevice(serviceAccount, existingDevice);

          if (!accountAuthResult)
          {
            logger.LogCritical("Service account is not authorized to install an agent on this device.");
            return Forbid();
          }
          break;

        case InstallerKeyCreatorKind.ServerServiceAccount:
          if (!await ValidateServerSa(installerKey.CreatorId, appDb))
          {
            logger.LogWarning("Server service account not found or disabled.");
            return Forbid();
          }

          serverSaValidated = true;
          break;

        default:
          throw new InvalidOperationException($"Unhandled installer key creator kind: {installerKey.CreatorKind}");
      }
    }

    var crossTenantDevice = await appDb.Devices
      .IgnoreQueryFilters()
      .FirstOrDefaultAsync(x => x.Id == deviceDto.Id && x.TenantId != tenantId);
    if (crossTenantDevice is not null)
    {
      logger.LogWarning("Device {DeviceId} already exists in a different tenant.", deviceDto.Id);
      return BadRequest("Device already exists in another tenant.");
    }

    if (requestDto.CustomerId is { } customerId)
    {
      var customerBelongsToTenant = await appDb.Customers
        .AnyAsync(x => x.Id == customerId && x.TenantId == tenantId);
      if (!customerBelongsToTenant)
      {
        logger.LogWarning(
          "Device {DeviceId} attempted to bind customer {CustomerId} outside tenant {TenantId}.",
          deviceDto.Id,
          customerId,
          tenantId);
        return BadRequest();
      }
    }

    if (requestDto.TagIds is { Count: > 0 })
    {
      // Tag assignment is device-scoped; the installer-key creator must hold DeviceTagsWrite
      // on the target device (server service accounts are trusted server-wide principals).
      var tagTarget = new Device
      {
        Id = deviceDto.Id,
        TenantId = tenantId,
        CustomerId = requestDto.CustomerId,
      };

      var canAssignTags = installerKey.CreatorKind switch
      {
        InstallerKeyCreatorKind.ServerServiceAccount => serverSaValidated || await ValidateServerSa(installerKey.CreatorId, appDb),
        InstallerKeyCreatorKind.User => await CanAssignTagsForUser(installerKey.CreatorId, tagTarget, userManager, deviceAuthorizationService),
        InstallerKeyCreatorKind.TenantServiceAccount => await CanAssignTagsForServiceAccount(installerKey.CreatorId, tenantId, tagTarget, appDb, deviceAuthorizationService),
        _ => false,
      };

      if (!canAssignTags)
      {
        logger.LogWarning("Installer key creator is not authorized to assign tags on device {DeviceId}.", deviceDto.Id);
        return Forbid();
      }
    }

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

    var entity = await deviceManager.AddOrUpdate(deviceDto, connectionContext, requestDto.TagIds, requestDto.PublicKey, requestDto.CustomerId);

    var isOutdated = await agentVersionProvider.IsAgentOutdated(entity.AgentVersion);
    return entity.ToInternalResponseDto(isOutdated);
  }

  private static async Task<bool> CanAssignTagsForServiceAccount(
    Guid creatorId,
    Guid tenantId,
    Device device,
    AppDb appDb,
    IDeviceAuthorizationService deviceAuthorizationService)
  {
    var serviceAccount = await appDb.ServiceAccounts
      .FirstOrDefaultAsync(x => x.Id == creatorId && x.TenantId == tenantId && x.IsEnabled);
    return serviceAccount is not null && await deviceAuthorizationService.CanAssignTagOnDevice(serviceAccount, device);
  }

  private static async Task<bool> CanAssignTagsForUser(
    Guid creatorId,
    Device device,
    UserManager<AppUser> userManager,
    IDeviceAuthorizationService deviceAuthorizationService)
  {
    var user = await userManager.FindByIdAsync($"{creatorId}");
    return user is not null && await deviceAuthorizationService.CanAssignTagOnDevice(user, device);
  }

  private static async Task<bool> ValidateServerSa(Guid creatorId, AppDb appDb)
  {
    // ServiceAccounts is polymorphic (server and tenant rows share the table). Restrict to a real
    // server account so a tenant service-account id can never satisfy the server-wide trust check.
    var serviceAccount = await appDb.ServiceAccounts
      .IgnoreQueryFilters()
      .FirstOrDefaultAsync(x => x.Id == creatorId && x.IsEnabled && x.Kind == ServiceAccountKind.Server);
    return serviceAccount is not null;
  }
}
