using ControlR.Libraries.Shared.Constants;
using ControlR.Web.Server.Services.DeviceManagement;
using Microsoft.AspNetCore.Mvc;

namespace ControlR.Web.Server.Api;

[Route(HttpConstants.DevicesEndpoint)]
[ApiController]
[Authorize]
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
    [FromServices] ILogger<DevicesController> logger)
  {
    using var logScope = logger.BeginScope(requestDto);
    var deviceDto = requestDto.Device;

    if (deviceDto.Id == Guid.Empty)
    {
      logger.LogWarning("Invalid device ID.");
      return BadRequest();
    }

    // Validate key without consuming usage - we'll consume at the end if all checks pass
    if (!await keyManager.ValidateKey(requestDto.InstallerKeyId, requestDto.InstallerKeySecret))
    {
      logger.LogWarning("Invalid installer key.");
      return BadRequest();
    }

    var getKeyResult = await keyManager.TryGetKey(requestDto.InstallerKeyId);
    if (!getKeyResult.IsSuccess)
    {
      logger.LogWarning("Error retrieving installer key: {Reason}", getKeyResult.Reason);
      return BadRequest();
    }

    var installerKey = getKeyResult.Value;

    var existingDevice = await appDb.Devices.FirstOrDefaultAsync(x => x.Id == deviceDto.Id);
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
    if (!await keyManager.ValidateAndConsumeKey(
      requestDto.InstallerKeyId,
      requestDto.InstallerKeySecret,
      deviceDto.Id,
      HttpContext.Connection.RemoteIpAddress?.ToString()))
    {
      logger.LogWarning("Failed to consume installer key usage.");
      return BadRequest();
    }

    var connectionContext = new DeviceConnectionContext(
      ConnectionId: string.Empty,
      RemoteIpAddress: HttpContext.Connection.RemoteIpAddress,
      LastSeen: DateTimeOffset.UtcNow,
      IsOnline: false);

    var entity = await deviceManager.AddOrUpdate(deviceDto, connectionContext, requestDto.TagIds);
    var isOutdated = await GetIsOutdated(entity, agentVersionProvider);
    return entity.ToDto(isOutdated);
  }

  [HttpDelete("{deviceId:guid}")]
  public async Task<IActionResult> DeleteDevice(
    [FromServices] AppDb appDb,
    [FromServices] IAuthorizationService authorizationService,
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

    appDb.Devices.Remove(device);
    await appDb.SaveChangesAsync();
    return NoContent();
  }

  [HttpGet]
  public async IAsyncEnumerable<DeviceResponseDto> Get(
    [FromServices] AppDb appDb,
    [FromServices] IAgentVersionProvider agentVersionProvider,
    [FromServices] IAuthorizationService authorizationService)
  {
    var deviceStream = appDb.Devices.AsAsyncEnumerable();

    await foreach (var device in deviceStream)
    {
      var authResult =
        await authorizationService.AuthorizeAsync(User, device, DeviceAccessByDeviceResourcePolicy.PolicyName);

      if (authResult.Succeeded)
      {
        var isOutdated = await GetIsOutdated(device, agentVersionProvider);
        yield return device.ToDto(isOutdated);
      }
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

  [HttpPost("search")]
  public async Task<ActionResult<DeviceSearchResponseDto>> SearchDevices(
    [FromBody] DeviceSearchRequestDto requestDto,
    [FromServices] AppDb appDb,
    [FromServices] IAuthorizationService authorizationService,
    [FromServices] IAgentVersionProvider agentVersionProvider,
    [FromServices] ILogger<DevicesController> logger)
  {
    var isRelationalDatabase = appDb.Database.IsRelational();
    // Start with all devices
    var anyDevices = await appDb.Devices.AnyAsync();
    var query = appDb.Devices
      .Include(x => x.Tags)
      .AsSplitQuery()
      .OrderBy(x => x.CreatedAt)
      .AsQueryable();

    // Apply filtering
    query = query
      .FilterBySearchText(requestDto.SearchText, isRelationalDatabase)
      .FilterByOnlineOffline(requestDto.HideOfflineDevices)
      .FilterByColumnFilters(requestDto.FilterDefinitions, isRelationalDatabase, logger);

    query = await query.FilterByTagIds(requestDto.TagIds, appDb);

    if (query is null)
    {
      // No matching devices found
      return new DeviceSearchResponseDto
      {
        Items = [],
        TotalItems = 0,
        AnyDevicesForUser = anyDevices
      };
    }

    // Apply sorting
    query = query.ApplySorting(requestDto.SortDefinitions, logger);

    // Get the total count of matching items (before pagination)
    var totalCount = await query.CountAsync();

    // Get the devices for the current page
    var devices = await query
      .Skip(requestDto.Page * requestDto.PageSize)
      .Take(requestDto.PageSize)
      .ToListAsync();

    // Filter for authorized devices
    var authorizedDevices = new List<DeviceResponseDto>();

    foreach (var device in devices)
    {
      var authResult = await authorizationService.AuthorizeAsync(
          User,
          device,
          DeviceAccessByDeviceResourcePolicy.PolicyName);

      if (authResult.Succeeded)
      {
        var isOutdated = await GetIsOutdated(device, agentVersionProvider);
        authorizedDevices.Add(device.ToDto(isOutdated));
      }
    }

    var response = new DeviceSearchResponseDto
    {
      Items = authorizedDevices,
      TotalItems = totalCount,
      AnyDevicesForUser = anyDevices
    };

    return response;
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
