using System.Linq.Expressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

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

  [HttpPost("grid")]
  [OutputCache(PolicyName = "DeviceGridPolicy")]
  public async Task<ActionResult<DeviceGridResponseDto>> GetDevicesGridData(
    [FromBody] DeviceGridRequestDto requestDto,
    [FromServices] AppDb appDb,
    [FromServices] IAuthorizationService authorizationService,
    [FromServices] ILogger<DevicesController> logger)
  {
    // Start with all devices
    var anyDevices = await appDb.Devices.AnyAsync();
    var query = appDb.Devices
      .Include(x => x.Tags)
      .AsSplitQuery()
      .OrderBy(x => x.CreatedAt)
      .AsQueryable();

    // Apply filtering
    if (!string.IsNullOrWhiteSpace(requestDto.SearchText))
    {
      var searchText = requestDto.SearchText;

      if (appDb.Database.IsInMemory())
      {
        query = query.Where(d =>
          d.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
          d.Alias.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
          d.OsDescription.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
          d.ConnectionId.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
          string.Join("", d.CurrentUsers).Contains(searchText, StringComparison.OrdinalIgnoreCase));
      }
      else
      {
        // Use EF.Functions.Like for the database-searchable fields
        query = query.Where(d =>
          EF.Functions.ILike(d.Name ?? "", $"%{searchText}%") ||
          EF.Functions.ILike(d.Alias ?? "", $"%{searchText}%") ||
          EF.Functions.ILike(d.OsDescription ?? "", $"%{searchText}%") ||
          EF.Functions.ILike(d.ConnectionId ?? "", $"%{searchText}%") ||
          EF.Functions.ILike(string.Join("", d.CurrentUsers) ?? "", $"%{searchText}%"));
      }
    }

    if (requestDto.HideOfflineDevices)
    {
      query = query.Where(d => d.IsOnline);
    }

    // Handle tag filtering
    if (requestDto.TagIds != null && requestDto.TagIds.Count > 0)
    {
      // Find devices through the many-to-many relationship
      var deviceIds = await appDb.Devices
          .Where(d => d.Tags!.Any(t => requestDto.TagIds.Contains(t.Id)))
          .Select(d => d.Id)
          .ToListAsync();

      if (deviceIds.Count != 0)
      {
        query = query.Where(d => deviceIds.Contains(d.Id));
      }
      else
      {
        // No matching devices found
        return new DeviceGridResponseDto
        {
          Items = [],
          TotalItems = 0,
          AnyDevicesForUser = anyDevices
        };
      }
    }

    var sortExpressions = new Dictionary<string, Expression<Func<Device, object>>>
    {
      [nameof(DeviceDto.Name)] = d => d.Name,
      [nameof(DeviceDto.IsOnline)] = d => d.IsOnline,
      [nameof(DeviceDto.CpuUtilization)] = d => d.CpuUtilization,
      [nameof(DeviceDto.UsedMemoryPercent)] = d => d.UsedMemoryPercent,
      [nameof(DeviceDto.UsedStoragePercent)] = d => d.UsedStoragePercent
    };

    if (requestDto.SortDefinitions != null && requestDto.SortDefinitions.Count > 0)
    {
      IOrderedQueryable<Device>? orderedQuery = null;

      foreach (var sortDef in requestDto.SortDefinitions.OrderBy(s => s.SortOrder))
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

    var response = new DeviceGridResponseDto
    {
      Items = authorizedDevices,
      TotalItems = totalCount,
      AnyDevicesForUser = anyDevices
    };

    return response;
  }
}
