using System.Collections.Immutable;
using ControlR.Web.Server.Authz.Permissions;
using ControlR.Web.Server.Extensions.Dtos.Internal;
using ControlR.Web.Server.Services.Authorization;
using ControlR.Web.Server.Services.DeviceManagement;
using Microsoft.AspNetCore.Mvc;

namespace ControlR.Web.Server.Api.Internal;

[Route(HttpConstants.Internal.DevicesEndpoint)]
[ApiController]
[Authorize]
[EndpointGroupName(OpenApiConstants.InternalGroupName)]
public class DevicesController(
  IDeviceAccessScopeResolver deviceAccessScopeResolver) : ControllerBase
{

  private readonly IDeviceAccessScopeResolver _deviceAccessScopeResolver = deviceAccessScopeResolver;

  [HttpDelete("{deviceId:guid}")]
  [ApiDeprecated("/api/v1/devices/{deviceId}", Note = "The replacement resolves the device across tenants and enforces tenant access before the resource policy.")]
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
      await authorizationService.AuthorizeAsync(User, device, DeviceResourcePolicies.Delete);
    if (!authResult.Succeeded)
    {
      return NotFound();
    }

    appDb.Devices.Remove(device);
    await appDb.SaveChangesAsync();
    return NoContent();
  }

  [HttpPost("delete-many")]
  [ApiDeprecated("/api/v1/devices/delete-many", Note = "The replacement evaluates tenant access per candidate device instead of pre-filtering by the caller's tenant claim, so server-scoped principals are supported.")]
  public async Task<ActionResult<InternalDtos.DeleteManyDevicesResponseDto>> DeleteMany(
    [FromServices] AppDb appDb,
    [FromServices] IPermissionEvaluator permissionEvaluator,
    [FromBody] InternalDtos.DeleteDevicesRequestDto requestDto,
    CancellationToken cancellationToken)
  {
    if (requestDto.DeviceIds.Count > DtoLimits.DeviceIdsMaxCount)
    {
      return BadRequest($"Too many device IDs. Maximum allowed is {DtoLimits.DeviceIdsMaxCount}.");
    }

    if (!User.TryGetTenantId(out var tenantId))
    {
      return BadRequest("Tenant ID not found.");
    }

    var candidateDevices = await appDb.Devices
      .AsNoTracking()
      .Include(x => x.DeviceGroupMembers)
      .Where(d => d.TenantId == tenantId && requestDto.DeviceIds.Contains(d.Id))
      .ToListAsync(cancellationToken);

    var principal = User.ToPrincipalDescriptor();
    if (principal is null)
    {
      return Forbid();
    }

    var requests = candidateDevices
      .Select(device => new PermissionEvaluationRequest(
        PermissionNames.DeviceDelete,
        new ResourceDescriptor(
          PermissionScopeKind.Device,
          device.Id,
          device.TenantId,
          device.CustomerId,
          [.. device.DeviceGroupMembers!.Select(member => member.DeviceGroupId)])))
      .ToList();

    var decisions = await permissionEvaluator.EvaluateBatch(principal, requests, cancellationToken);

    var authorizedIdSet = new HashSet<Guid>();
    for (var i = 0; i < candidateDevices.Count; i++)
    {
      if (decisions[requests[i]].Allowed)
      {
        authorizedIdSet.Add(candidateDevices[i].Id);
      }
    }

    var deletedCount = await appDb.Devices
      .Where(x => x.TenantId == tenantId && authorizedIdSet.Contains(x.Id))
      .ExecuteDeleteAsync(cancellationToken);

    if (deletedCount == authorizedIdSet.Count)
    {
      // All authorized devices were deleted.
      return new InternalDtos.DeleteManyDevicesResponseDto(
        SuccessIds: [.. authorizedIdSet],
        FailureIds: [.. requestDto.DeviceIds.Except(authorizedIdSet)]);
    }

    var remainingIds = await appDb.Devices
      .Where(x => x.TenantId == tenantId && authorizedIdSet.Contains(x.Id))
      .Select(x => x.Id)
      .ToListAsync(cancellationToken);

    var successIds = authorizedIdSet.Except(remainingIds).ToImmutableList();
    var failureIds = remainingIds.Concat(requestDto.DeviceIds.Except(authorizedIdSet)).ToImmutableList();

    return new InternalDtos.DeleteManyDevicesResponseDto(successIds, failureIds);
  }

  [HttpGet]
  [ApiDeprecated("/api/v1/devices")]
  public async IAsyncEnumerable<InternalDtos.DeviceResponseDto> Get(
    [FromServices] AppDb appDb,
    [FromServices] IAgentVersionProvider agentVersionProvider)
  {
    IQueryable<Device> query = await appDb.Devices
      .Include(x => x.Tags)
      .Include(x => x.Customer)
      .AsSplitQuery()
      .ApplyDeviceAccessScope(User, _deviceAccessScopeResolver);

    var (isSuccess, agentVersion) = await GetAgentVersion(agentVersionProvider);

    await foreach (var device in query.AsAsyncEnumerable())
    {
      var isOutdated = isSuccess && device.AgentVersion != agentVersion;
      yield return device.ToInternalResponseDto(isOutdated);
    }
  }

  [HttpGet("{deviceId:guid}")]
  [ApiDeprecated("/api/v1/devices/{deviceId}", Note = "The replacement resolves the device across tenants and enforces tenant access before the resource policy.")]
  public async Task<ActionResult<InternalDtos.DeviceResponseDto>> GetDevice(
    [FromServices] AppDb appDb,
    [FromServices] IAuthorizationService authorizationService,
    [FromServices] IAgentVersionProvider agentVersionProvider,
    [FromRoute] Guid deviceId)
  {
    var device = await appDb.Devices
      .AsNoTracking()
      .Include(x => x.Customer)
      .FirstOrDefaultAsync(x => x.Id == deviceId);
      
    if (device is null)
    {
      return NotFound();
    }

    var authResult =
      await authorizationService.AuthorizeAsync(User, device, DeviceResourcePolicies.Read);

    if (!authResult.Succeeded)
    {
      return NotFound();
    }

    var isOutdated = await agentVersionProvider.IsAgentOutdated(device.AgentVersion);
    return device.ToInternalResponseDto(isOutdated);
  }

  [HttpGet("summary")]
  [ApiDeprecated("/api/v1/devices/summary")]
  public async IAsyncEnumerable<InternalDtos.DeviceSummaryDto> GetDeviceSummaries(
    [FromServices] AppDb appDb)
  {
    IQueryable<Device> query = await appDb.Devices
      .ApplyDeviceAccessScope(User, _deviceAccessScopeResolver);

    await foreach (var device in query.AsAsyncEnumerable())
    {
      yield return device.ToInternalSummaryDto();
    }
  }

  [HttpPost("search")]
  [ApiDeprecated("/api/v1/devices/search", Note = "The replacement evaluates device access via the caller's access scope without requiring a tenant claim, so server-scoped principals are supported.")]
  public async Task<ActionResult<InternalDtos.DeviceSearchResponseDto>> SearchDevices(
    [FromBody] InternalDtos.DeviceSearchRequestDto requestDto,
    [FromServices] AppDb appDb,
    [FromServices] IAgentVersionProvider agentVersionProvider,
    [FromServices] ILogger<DevicesController> logger)
  {
    var authorizedQuery = await appDb.Devices
      .ApplyDeviceAccessScope(User, _deviceAccessScopeResolver);

    var isRelationalDatabase = appDb.Database.IsRelational();
    var anyDevices = await authorizedQuery.AnyAsync();

    var filteredQuery = authorizedQuery
      .FilterBySearchText(requestDto.SearchText, isRelationalDatabase)
      .FilterByOnlineOffline(requestDto.HideOfflineDevices)
      .FilterByColumnFilters(requestDto.FilterDefinitions, isRelationalDatabase, logger)
      .FilterByCustomerIds(requestDto.CustomerIds);

    var scopedQuery = filteredQuery.FilterByTagsAndDeviceGroups(
      requestDto.TagIds,
      requestDto.TagFilterMatchMode,
      requestDto.DeviceGroupIds,
      requestDto.DeviceGroupFilterMatchMode,
      requestDto.ShowOnlyUntaggedDevices,
      requestDto.ShowOnlyUngroupedDevices);

    var filterCounts = await GetFilterCounts(scopedQuery);
    var totalCount = await scopedQuery.CountAsync();

    // Prevent int overflow in Skip, which would produce a negative SQL OFFSET.
    var clampedPageSize = Math.Max(1, requestDto.PageSize);
    var clampedPage = Math.Clamp(requestDto.Page, 0, int.MaxValue / clampedPageSize);

    var devices = await scopedQuery
      .ApplySorting(requestDto.SortDefinitions)
      .Include(x => x.Tags)
      .Include(x => x.Customer)
      .AsSplitQuery()
      .Skip(clampedPage * clampedPageSize)
      .Take(clampedPageSize)
      .ToListAsync();


    var (isSuccess, currentAgentVersion) = await GetAgentVersion(agentVersionProvider);

    var pagedDtos = new List<InternalDtos.DeviceResponseDto>(devices.Count);
    foreach (var device in devices)
    {
      var isOutdated = isSuccess && device.AgentVersion != currentAgentVersion;
      pagedDtos.Add(device.ToInternalResponseDto(isOutdated));
    }

    var response = new InternalDtos.DeviceSearchResponseDto
    {
      AnyDevicesForUser = anyDevices,
      FilterCounts = filterCounts,
      Items = pagedDtos,
      TotalItems = totalCount
    };

    return response;
  }

  [HttpPatch("{deviceId:guid}/alias")]
  [Authorize]
  [ApiDeprecated("/api/v1/devices/{deviceId}/alias", Note = "The replacement resolves the device across tenants and enforces tenant access before the resource policy.")]
  public async Task<ActionResult<InternalDtos.DeviceResponseDto>> UpdateDeviceAlias(
    [FromRoute] Guid deviceId,
    [FromBody] InternalDtos.UpdateDeviceAliasRequestDto requestDto,
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
      await authorizationService.AuthorizeAsync(User, device, DeviceResourcePolicies.AliasWrite);
    if (!authResult.Succeeded)
    {
      logger.LogWarning("User {UserName} denied access to update alias for device {DeviceId}.", User.Identity?.Name, deviceId);
      return NotFound();
    }

    device.Alias = requestDto.Alias ?? string.Empty;
    await appDb.SaveChangesAsync();

    var isOutdated = await agentVersionProvider.IsAgentOutdated(device.AgentVersion);
    return device.ToInternalResponseDto(isOutdated);
  }

  private static async Task<(bool IsSuccess, string Version)> GetAgentVersion(IAgentVersionProvider agentVersionProvider)
  {
    var agentVersionResult = await agentVersionProvider.TryGetAgentVersion();
    if (!agentVersionResult.IsSuccess)
    {
      return (false, string.Empty);
    }
    return (true, agentVersionResult.Value.ToString());
  }

  private static async Task<InternalDtos.DeviceSearchFilterCountsDto> GetFilterCounts(IQueryable<Device> query)
  {
    return await query
      .Select(x => new { IsTagged = x.Tags!.Any(), x.IsOnline })
      .GroupBy(_ => 1)
      .OrderBy(g => g.Key)
      .Select(group => new InternalDtos.DeviceSearchFilterCountsDto
      {
        TaggedDevices = group.Count(x => x.IsTagged),
        UntaggedDevices = group.Count(x => !x.IsTagged),
        OnlineDevices = group.Count(x => x.IsOnline),
        OfflineDevices = group.Count(x => !x.IsOnline)
      })
      .FirstOrDefaultAsync()
      ?? new InternalDtos.DeviceSearchFilterCountsDto();
  }
}
