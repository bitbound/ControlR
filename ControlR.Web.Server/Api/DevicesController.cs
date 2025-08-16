using System.Linq.Expressions;
using Microsoft.AspNetCore.Mvc;
using MudBlazor;

namespace ControlR.Web.Server.Api;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class DevicesController : ControllerBase
{
  [HttpPost]
  [AllowAnonymous]
  public async Task<ActionResult<DeviceDto>> CreateDevice(
    [FromBody] CreateDeviceRequestDto requestDto,
    [FromServices] AppDb appDb,
    [FromServices] UserManager<AppUser> userManager,
    [FromServices] IAgentInstallerKeyManager keyManager,
    [FromServices] IDeviceManager deviceManager,
    [FromServices] ILogger<DevicesController> logger)
  {
    using var logScope = logger.BeginScope(requestDto);
    var deviceDto = requestDto.Device;

    if (deviceDto.Id == Guid.Empty)
    {
      logger.LogWarning("Invalid device ID.");
      return BadRequest();
    }

    if (!keyManager.TryGetKey(requestDto.InstallerKey, out var installerKey))
    {
      logger.LogWarning("Installer key not found.");
      return BadRequest();
    }

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

    if (!await keyManager.ValidateKey(requestDto.InstallerKey))
    {
      logger.LogWarning("Invalid installer key.");
      return BadRequest();
    }
    // Device shouldn't be considered online until it connects to the AgentHub.
    deviceDto = deviceDto with { IsOnline = false };
    var entity = await deviceManager.AddOrUpdate(deviceDto, addTagIds: true);
    return entity.ToDto();
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
  public async IAsyncEnumerable<DeviceDto> Get(
    [FromServices] AppDb appDb,
    [FromServices] IAuthorizationService authorizationService)
  {
    var deviceStream = appDb.Devices.AsAsyncEnumerable();

    await foreach (var device in deviceStream)
    {
      var authResult =
        await authorizationService.AuthorizeAsync(User, device, DeviceAccessByDeviceResourcePolicy.PolicyName);
      if (authResult.Succeeded)
      {
        yield return device.ToDto();
      }
    }
  }

  [HttpPost("search")]
  public async Task<ActionResult<DeviceSearchResponseDto>> SearchDevices(
    [FromBody] DeviceSearchRequestDto requestDto,
    [FromServices] AppDb appDb,
    [FromServices] IAuthorizationService authorizationService,
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
    var sortExpressions = new Dictionary<string, Expression<Func<Device, object>>>
    {
      [nameof(DeviceDto.Name)] = d => d.Name,
      [nameof(DeviceDto.IsOnline)] = d => d.IsOnline,
      [nameof(DeviceDto.CpuUtilization)] = d => d.CpuUtilization,
      [nameof(DeviceDto.UsedMemoryPercent)] = d => d.UsedMemoryPercent,
      [nameof(DeviceDto.UsedStoragePercent)] = d => d.UsedStoragePercent
    };

    if (requestDto.SortDefinitions is { Count: > 0 } sortDefs)
    {
      IOrderedQueryable<Device>? orderedQuery = null;

      foreach (var sortDef in sortDefs.OrderBy(s => s.SortOrder))
      {
        if (string.IsNullOrWhiteSpace(sortDef.PropertyName) ||
            !sortExpressions.TryGetValue(sortDef.PropertyName, out var expr))
        {

          continue;
        }

        if (orderedQuery == null)
        {
          orderedQuery = sortDef.Descending
              ? query.OrderByDescending(expr)
              : query.OrderBy(expr);
        }
        else
        {
          orderedQuery = sortDef.Descending
              ? orderedQuery.ThenByDescending(expr)
              : orderedQuery.ThenBy(expr);
        }
      }

      query = orderedQuery ?? query;
    }

    // Get the total count of matching items (before pagination)
    var totalCount = await query.CountAsync();

    // Get the devices for the current page
    var devices = await query
      .Skip(requestDto.Page * requestDto.PageSize)
      .Take(requestDto.PageSize)
      .ToListAsync();

    // Filter for authorized devices
    var authorizedDevices = new List<DeviceDto>();

    foreach (var device in devices)
    {
      var authResult = await authorizationService.AuthorizeAsync(
          User,
          device,
          DeviceAccessByDeviceResourcePolicy.PolicyName);

      if (authResult.Succeeded)
      {
        authorizedDevices.Add(device.ToDto());
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
}
