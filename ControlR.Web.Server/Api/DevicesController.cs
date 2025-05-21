using System.Security.Claims;
using ControlR.Web.Server.Authz.Policies;
using ControlR.Web.Server.Data;
using ControlR.Web.Server.Data.Entities;
using ControlR.Web.Server.Interfaces;
using ControlR.Web.Server.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;

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
    try
    {
      logger.LogDebug("Processing device grid request: UserId: {UserId}, Page {Page}, PageSize {PageSize}, Search: {SearchText}, HideOffline: {HideOffline}, TagIds: {TagIds}",
          User.FindFirst(ClaimTypes.NameIdentifier)?.Value, requestDto.Page, requestDto.PageSize, requestDto.SearchText, requestDto.HideOfflineDevices,
          requestDto.TagIds != null ? string.Join(",", requestDto.TagIds) : "none");

      var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

      // Start with all devices
      var query = appDb.Devices.AsQueryable();

      // Apply filtering
      if (!string.IsNullOrWhiteSpace(requestDto.SearchText))
      {
        var searchText = requestDto.SearchText;

        // Use EF.Functions.Like for the database-searchable fields
        query = query.Where(d =>
          EF.Functions.ILike(d.Name ?? "", $"%{searchText}%") ||
          EF.Functions.ILike(d.Alias ?? "", $"%{searchText}%") ||
          EF.Functions.ILike(d.OsDescription ?? "", $"%{searchText}%") ||
          EF.Functions.ILike(d.ConnectionId ?? "", $"%{searchText}%") ||
          d.CurrentUsers.Any(x => EF.Functions.ILike(x, $"%{searchText}%")));

        // We'll handle arrays in memory after retrieving the base data
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
            TotalItems = 0
          };
        }
      }

      // Apply sorting
      if (requestDto.SortDefinitions != null && requestDto.SortDefinitions.Count > 0)
      {
        IOrderedQueryable<Device>? orderedQuery = null;

        foreach (var sortDef in requestDto.SortDefinitions.OrderBy(s => s.SortOrder))
        {
          Func<IQueryable<Device>, IOrderedQueryable<Device>> orderFunc;

          switch (sortDef.PropertyName)
          {
            case nameof(Device.Name):
              orderFunc = q => sortDef.Descending ? q.OrderByDescending(d => d.Name) : q.OrderBy(d => d.Name);
              break;
            case nameof(Device.IsOnline):
              orderFunc = q => sortDef.Descending ? q.OrderByDescending(d => d.IsOnline) : q.OrderBy(d => d.IsOnline);
              break;
            case "CpuUtilization":
              orderFunc = q => sortDef.Descending ? q.OrderByDescending(d => d.CpuUtilization) : q.OrderBy(d => d.CpuUtilization);
              break;
            case "UsedMemoryPercent":
              orderFunc = q => sortDef.Descending ?
                q.OrderByDescending(d => d.UsedMemory / (double)(d.TotalMemory == 0 ? 1 : d.TotalMemory)) :
                q.OrderBy(d => d.UsedMemory / (double)(d.TotalMemory == 0 ? 1 : d.TotalMemory));
              break;
            case "UsedStoragePercent":
              orderFunc = q => sortDef.Descending ?
                q.OrderByDescending(d => d.UsedStorage / (double)(d.TotalStorage == 0 ? 1 : d.TotalStorage)) :
                q.OrderBy(d => d.UsedStorage / (double)(d.TotalStorage == 0 ? 1 : d.TotalStorage));
              break;
            default:
              continue;
          }

          orderedQuery = orderedQuery == null ? orderFunc(query) : orderFunc(orderedQuery);
        }

        query = orderedQuery ?? query;
      }

      // Get the total count of matching items (before pagination)
      var totalCount = await query.CountAsync();

      // Get the devices for the current page
      var devices = await query
        .Skip(requestDto.Page * requestDto.PageSize)
        .Take(requestDto.PageSize)
        .Include(d => d.Tags) // Pre-load tags to avoid N+1 issues
        .ToListAsync();

      // Filter for authorized devices
      var authorizedDevices = new List<DeviceDto>();

      foreach (var device in devices)
      {
        var authResult = await authorizationService.AuthorizeAsync(
            User, device, DeviceAccessByDeviceResourcePolicy.PolicyName);

        if (authResult.Succeeded)
        {
          authorizedDevices.Add(device.ToDto());
        }
      }

      var response = new DeviceGridResponseDto
      {
        Items = authorizedDevices,
        TotalItems = totalCount
      };

      logger.LogDebug("Returning device grid data: Total: {TotalItems}, Returned: {ItemCount}, Page: {Page}, PageSize: {PageSize}",
          response.TotalItems, response.Items.Count, requestDto.Page, requestDto.PageSize);

      return response;
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error retrieving device grid data: {ErrorMessage}", ex.Message);
      return StatusCode(500, "An error occurred while retrieving device data");
    }
  }
}
