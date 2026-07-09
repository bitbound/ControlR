using System.Collections.Immutable;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Shared.Services.Encryption;
using ControlR.Web.Server.Services.AgentInstaller;
using ControlR.Web.Server.Services.DeviceManagement;
using Microsoft.AspNetCore.Mvc;

namespace ControlR.Web.Server.Api;

[Route(HttpConstants.DevicesEndpoint)]
[ApiController]
[Authorize]
public class DevicesController(
  IDeviceAccessScopeResolver deviceAccessScopeResolver) : ControllerBase
{
  private readonly IDeviceAccessScopeResolver _deviceAccessScopeResolver = deviceAccessScopeResolver;

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
        return Unauthorized();
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
    return entity.ToDto(isOutdated);
  }

  [HttpDelete("{deviceId:guid}")]
  public async Task<IActionResult> DeleteDevice(
    [FromServices] AppDb appDb,
    [FromServices] IAuthorizationService authorizationService,
    [FromRoute] Guid deviceId)
  {
    if (!User.TryGetTenantId(out var tenantId))
    {
      return BadRequest("Tenant ID not found.");
    }

    var device = await appDb.Devices.FirstOrDefaultAsync(
        x => x.Id == deviceId && x.TenantId == tenantId);

    if (device is null)
    {
      return NotFound();
    }

    // Single-device operations use the resource policy directly.
    var authResult =
      await authorizationService.AuthorizeAsync(User, device, DeviceAccessByDeviceResourcePolicy.PolicyName);
    if (!authResult.Succeeded)
    {
      return Forbid();
    }

    appDb.Devices.Remove(device);
    await appDb.SaveChangesAsync();
    return NoContent();
  }

  [HttpPost("delete-many")]
  public async Task<ActionResult<DeleteManyDevicesResponseDto>> DeleteMany(
    [FromServices] AppDb appDb,
    [FromBody] DeleteDevicesRequestDto requestDto,
    CancellationToken cancellationToken)
  {
    if (!User.TryGetTenantId(out var tenantId))
    {
      return BadRequest("Tenant ID not found.");
    }

    var accessScope = await _deviceAccessScopeResolver.Resolve(User, tenantId, cancellationToken);

    // Authorized + existing devices (subset of input that user can delete).
    var authorizedDeviceIds = await appDb.Devices
      .ApplyAccessScope(tenantId, accessScope)
      .Where(d => requestDto.DeviceIds.Contains(d.Id))
      .Select(d => d.Id)
      .ToListAsync(cancellationToken);

    var authorizedIdSet = authorizedDeviceIds.ToHashSet();

    var deletedCount = await appDb.Devices
      .Where(x => x.TenantId == tenantId && authorizedIdSet.Contains(x.Id))
      .ExecuteDeleteAsync(cancellationToken);

    if (deletedCount == authorizedDeviceIds.Count)
    {
      // All authorized devices were deleted.
      return new DeleteManyDevicesResponseDto(
        SuccessIds: [.. authorizedDeviceIds],
        FailureIds: [.. requestDto.DeviceIds.Except(authorizedIdSet)]);
    }

    var remainingIds = await appDb.Devices
      .Where(x => x.TenantId == tenantId && authorizedIdSet.Contains(x.Id))
      .Select(x => x.Id)
      .ToListAsync(cancellationToken);

    var successIds = authorizedIdSet.Except(remainingIds).ToImmutableList();
    var failureIds = remainingIds.Concat(requestDto.DeviceIds.Except(authorizedIdSet)).ToImmutableList();

    return new DeleteManyDevicesResponseDto(successIds, failureIds);
  }

  [HttpGet]
  public async IAsyncEnumerable<DeviceResponseDto> Get(
    [FromServices] AppDb appDb,
    [FromServices] IAgentVersionProvider agentVersionProvider)
  {
    IQueryable<Device> query = appDb.Devices.Include(x => x.Tags);

    if (!User.TryGetTenantId(out var tenantId))
    {
      yield break;
    }

    var accessScope = await _deviceAccessScopeResolver.Resolve(User, tenantId);
    query = query
      .ApplyAccessScope(tenantId, accessScope)
      .AsSplitQuery()
      .OrderBy(x => x.CreatedAt);

    var deviceStream = query.AsAsyncEnumerable();

    await foreach (var device in deviceStream)
    {
      var isOutdated = await GetIsOutdated(device, agentVersionProvider);
      yield return device.ToDto(isOutdated);
    }
  }

  [HttpGet("{deviceId:guid}")]
  public async Task<ActionResult<DeviceResponseDto>> GetDevice(
    [FromServices] AppDb appDb,
    [FromServices] IAuthorizationService authorizationService,
    [FromServices] IAgentVersionProvider agentVersionProvider,
    [FromRoute] Guid deviceId)
  {
    var device = await appDb.Devices.FirstOrDefaultAsync(x => x.Id == deviceId);
    if (device is null)
    {
      return NotFound();
    }

    var authResult =
      await authorizationService.AuthorizeAsync(User, device, DeviceAccessByDeviceResourcePolicy.PolicyName);

    if (!authResult.Succeeded)
    {
      return Forbid();
    }

    var isOutdated = await GetIsOutdated(device, agentVersionProvider);
    return device.ToDto(isOutdated);
  }

  [HttpGet("summary")]
  public async IAsyncEnumerable<DeviceSummaryDto> GetDeviceSummaries(
    [FromServices] AppDb appDb)
  {
    IQueryable<Device> query = appDb.Devices;

    if (!User.TryGetTenantId(out var tenantId))
    {
      yield break;
    }

    var accessScope = await _deviceAccessScopeResolver.Resolve(User, tenantId);
    query = query.ApplyAccessScope(tenantId, accessScope).OrderBy(x => x.CreatedAt);

    var deviceStream = query.AsAsyncEnumerable();

    await foreach (var device in deviceStream)
    {
      yield return device.ToSummaryDto();
    }
  }

  [HttpPost("search")]
  public async Task<ActionResult<DeviceSearchResponseDto>> SearchDevices(
    [FromBody] DeviceSearchRequestDto requestDto,
    [FromServices] AppDb appDb,
    [FromServices] IAgentVersionProvider agentVersionProvider,
    [FromServices] ILogger<DevicesController> logger)
  {
    if (!User.TryGetTenantId(out var tenantId))
    {
      return BadRequest("Tenant ID not found.");
    }

    var accessScope = await _deviceAccessScopeResolver.Resolve(User, tenantId);

    var isRelationalDatabase = appDb.Database.IsRelational();
    var authorizedQuery = appDb.Devices.ApplyAccessScope(tenantId, accessScope!).AsQueryable();

    var anyDevices = await authorizedQuery.AnyAsync();

    var filteredQuery = authorizedQuery
      .FilterBySearchText(requestDto.SearchText, isRelationalDatabase)
      .FilterByOnlineOffline(requestDto.HideOfflineDevices)
      .FilterByColumnFilters(requestDto.FilterDefinitions, isRelationalDatabase, logger);

    var hiddenUntaggedDevices = requestDto.IncludeUntaggedDevices
      ? 0
      : await filteredQuery.CountAsync(x => !x.Tags!.Any());

    var scopedQuery = filteredQuery.FilterByTagIds(requestDto.TagIds, requestDto.IncludeUntaggedDevices);
    var filterCounts = await GetFilterCounts(scopedQuery);
    var totalCount = await scopedQuery.CountAsync();

    var devices = await scopedQuery
      .ApplySorting(requestDto.SortDefinitions)
      .Include(x => x.Tags)
      .AsSplitQuery()
      .Skip(requestDto.Page * requestDto.PageSize)
      .Take(requestDto.PageSize)
      .ToListAsync();

    var pagedDtos = new List<DeviceResponseDto>(devices.Count);
    foreach (var device in devices)
    {
      var isOutdated = await GetIsOutdated(device, agentVersionProvider);
      pagedDtos.Add(device.ToDto(isOutdated));
    }

    var response = new DeviceSearchResponseDto
    {
      AnyDevicesForUser = anyDevices,
      FilterCounts = filterCounts,
      HiddenUntaggedDevices = hiddenUntaggedDevices,
      Items = pagedDtos,
      TotalItems = totalCount
    };

    return response;
  }

  [HttpPatch("{deviceId:guid}/alias")]
  [Authorize]
  public async Task<ActionResult<DeviceResponseDto>> UpdateDeviceAlias(
    [FromRoute] Guid deviceId,
    [FromBody] UpdateDeviceAliasRequestDto requestDto,
    [FromServices] AppDb appDb,
    [FromServices] IAuthorizationService authorizationService,
    [FromServices] IAgentVersionProvider agentVersionProvider,
    [FromServices] ILogger<DevicesController> logger)
  {
    if (deviceId != requestDto.DeviceId)
    {
      return BadRequest("Device ID mismatch.");
    }

    if (requestDto.Alias is not null && requestDto.Alias.Length > 100)
    {
      return BadRequest("Alias must be 100 characters or fewer.");
    }

    var device = await appDb.Devices.FirstOrDefaultAsync(x => x.Id == deviceId);
    if (device is null)
    {
      logger.LogWarning("Device {DeviceId} not found for alias update.", deviceId);
      return NotFound();
    }

    var authResult =
      await authorizationService.AuthorizeAsync(User, device, DeviceAccessByDeviceResourcePolicy.PolicyName);
    if (!authResult.Succeeded)
    {
      logger.LogWarning("User {UserName} denied access to update alias for device {DeviceId}.", User.Identity?.Name, deviceId);
      return Forbid();
    }

    device.Alias = requestDto.Alias ?? string.Empty;
    await appDb.SaveChangesAsync();

    var isOutdated = await GetIsOutdated(device, agentVersionProvider);
    return device.ToDto(isOutdated);
  }

  private static async Task<DeviceSearchFilterCountsDto> GetFilterCounts(IQueryable<Device> query)
  {
    return await query
      .Select(x => new { IsTagged = x.Tags!.Any(), x.IsOnline })
      .GroupBy(_ => 1)
      .Select(group => new DeviceSearchFilterCountsDto
      {
        TaggedDevices = group.Count(x => x.IsTagged),
        UntaggedDevices = group.Count(x => !x.IsTagged),
        OnlineDevices = group.Count(x => x.IsOnline),
        OfflineDevices = group.Count(x => !x.IsOnline)
      })
      .FirstOrDefaultAsync()
      ?? new DeviceSearchFilterCountsDto();
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
