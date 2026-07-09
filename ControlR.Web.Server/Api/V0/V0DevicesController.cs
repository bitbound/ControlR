using System.Collections.Immutable;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Shared.Services.Encryption;
using ControlR.Web.Server.Services.AgentInstaller;
using ControlR.Web.Server.Services.DeviceManagement;
using Microsoft.AspNetCore.Mvc;

namespace ControlR.Web.Server.Api.V0;

[Route(HttpConstants.V0.DevicesEndpoint)]
[ApiController]
[Authorize(Policy = RequireServerServiceAccountPolicy.PolicyName)]
public class V0DevicesController() : ControllerBase
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
    [FromServices] ILogger<V0DevicesController> logger,
    [FromServices] IEd25519KeyProvider keyProvider)
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
    [FromRoute] Guid deviceId)
  {
    var device = await appDb.Devices.FirstOrDefaultAsync(x => x.Id == deviceId);
    if (device is null)
    {
      return NotFound();
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
    var authorizedDeviceIds = await appDb.Devices
      .Where(d => requestDto.DeviceIds.Contains(d.Id))
      .Select(d => d.Id)
      .ToListAsync(cancellationToken);

    var authorizedIdSet = authorizedDeviceIds.ToHashSet();

    var deletedCount = await appDb.Devices
      .Where(x => authorizedIdSet.Contains(x.Id))
      .ExecuteDeleteAsync(cancellationToken);

    if (deletedCount == authorizedDeviceIds.Count)
    {
      return new DeleteManyDevicesResponseDto(
        SuccessIds: [.. authorizedDeviceIds],
        FailureIds: [.. requestDto.DeviceIds.Except(authorizedIdSet)]);
    }

    var remainingIds = await appDb.Devices
      .Where(x => authorizedIdSet.Contains(x.Id))
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
    var query = appDb.Devices
      .Include(x => x.Tags)
      .AsSplitQuery()
      .OrderBy(x => x.CreatedAt);

    await foreach (var device in query.AsAsyncEnumerable())
    {
      var isOutdated = await GetIsOutdated(device, agentVersionProvider);
      yield return device.ToV0Dto(isOutdated);
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
    return device.ToV0Dto(isOutdated);
  }

  [HttpGet("summary")]
  public async IAsyncEnumerable<DeviceSummaryDto> GetDeviceSummaries(
    [FromServices] AppDb appDb)
  {
    var query = appDb.Devices.OrderBy(x => x.CreatedAt);

    await foreach (var device in query.AsAsyncEnumerable())
    {
      yield return device.ToV0SummaryDto();
    }
  }

  [HttpPost("search")]
  public async Task<ActionResult<DeviceSearchResponseDto>> SearchDevices(
    [FromBody] DeviceSearchRequestDto requestDto,
    [FromServices] AppDb appDb,
    [FromServices] IAgentVersionProvider agentVersionProvider,
    [FromServices] ILogger<V0DevicesController> logger)
  {
    var isRelationalDatabase = appDb.Database.IsRelational();
    var authorizedQuery = appDb.Devices.AsQueryable();
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
      pagedDtos.Add(device.ToV0Dto(isOutdated));
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
  public async Task<ActionResult<DeviceResponseDto>> UpdateDeviceAlias(
    [FromRoute] Guid deviceId,
    [FromBody] UpdateDeviceAliasRequestDto requestDto,
    [FromServices] AppDb appDb,
    [FromServices] IAuthorizationService authorizationService,
    [FromServices] IAgentVersionProvider agentVersionProvider,
    [FromServices] ILogger<V0DevicesController> logger)
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
    return device.ToV0Dto(isOutdated);
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
