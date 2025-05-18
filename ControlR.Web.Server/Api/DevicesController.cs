using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

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

  [HttpGet("paged")]
  public async Task<ActionResult<GridData<DeviceDto>>> GetDevices(
    [FromQuery] int page,
    [FromQuery] int pageSize,
    [FromQuery] string? searchText,
    [FromQuery] string? sortBy,
    [FromQuery] bool descending,
    [FromServices] AppDb appDb,
    [FromServices] IAuthorizationService authorizationService)
  {
    var query = appDb.Devices.AsQueryable();

    if (!string.IsNullOrWhiteSpace(searchText))
    {
      query = query.Where(device =>
        device.Name.Contains(searchText) ||
        device.Alias.Contains(searchText) ||
        device.AgentVersion.Contains(searchText) ||
        device.OsDescription.Contains(searchText) ||
        device.PublicIpV4.Contains(searchText) ||
        device.PublicIpV6.Contains(searchText));
    }

    if (!string.IsNullOrWhiteSpace(sortBy))
    {
      query = descending
        ? query.OrderByDescending(sortBy)
        : query.OrderBy(sortBy);
    }

    var totalItems = await query.CountAsync();
    var devices = await query
      .Skip(page * pageSize)
      .Take(pageSize)
      .ToListAsync();

    var authorizedDevices = new List<DeviceDto>();
    foreach (var device in devices)
    {
      var authResult =
        await authorizationService.AuthorizeAsync(User, device, DeviceAccessByDeviceResourcePolicy.PolicyName);
      if (authResult.Succeeded)
      {
        authorizedDevices.Add(device.ToDto());
      }
    }

    return new GridData<DeviceDto>
    {
      TotalItems = totalItems,
      Items = authorizedDevices
    };
  }

  [HttpGet("count")]
  public async Task<ActionResult<int>> GetDeviceCount(
    [FromQuery] string? searchText,
    [FromServices] AppDb appDb)
  {
    var query = appDb.Devices.AsQueryable();

    if (!string.IsNullOrWhiteSpace(searchText))
    {
      query = query.Where(device =>
        device.Name.Contains(searchText) ||
        device.Alias.Contains(searchText) ||
        device.AgentVersion.Contains(searchText) ||
        device.OsDescription.Contains(searchText) ||
        device.PublicIpV4.Contains(searchText) ||
        device.PublicIpV6.Contains(searchText));
    }

    var totalItems = await query.CountAsync();
    return totalItems;
  }
}
