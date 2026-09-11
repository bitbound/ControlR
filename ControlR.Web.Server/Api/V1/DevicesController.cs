using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Asp.Versioning;
using ControlR.Libraries.Api.Contracts.Hubs.Clients;
using ControlR.Web.Server.Authz.Permissions;
using ControlR.Web.Server.Extensions.Dtos.V1;
using ControlR.Web.Server.Services.Authorization;
using ControlR.Web.Server.Services.Authorization.Capabilities;
using ControlR.Web.Server.Services.DeviceManagement;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using DeviceResponseDto = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.DeviceResponseDto;

namespace ControlR.Web.Server.Api.V1;

[Route(HttpConstants.V1.DevicesEndpoint)]
[ApiController]
[Authorize]
[ApiVersion(ApiVersions.V1)]
public class DevicesController(IDeviceAccessScopeResolver deviceAccessScopeResolver) : ControllerBase
{
  private readonly IDeviceAccessScopeResolver _deviceAccessScopeResolver = deviceAccessScopeResolver;

  [HttpDelete("{deviceId:guid}")]
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<IActionResult> DeleteDevice(
    [FromServices] AppDb appDb,
    [FromServices] IAuthorizationService authorizationService,
    [FromRoute] Guid deviceId,
    CancellationToken cancellationToken)
  {
    var device = await appDb.Devices.FirstOrDefaultAsync(x => x.Id == deviceId, cancellationToken);
    if (device is null)
    {
      return NotFound();
    }

    if (!User.CanAccessTenant(device.TenantId))
    {
      return NotFound();
    }

    var authResult = await authorizationService.AuthorizeAsync(User, device, DeviceResourcePolicies.Delete);
    if (!authResult.Succeeded)
    {
      return NotFound();
    }

    appDb.Devices.Remove(device);
    await appDb.SaveChangesAsync(cancellationToken);
    return NoContent();
  }

  [HttpPost("delete-many")]
  public async Task<ActionResult<V1Dtos.DeleteManyDevicesResponseDto>> DeleteMany(
    [FromServices] AppDb appDb,
    [FromServices] IPermissionEvaluator permissionEvaluator,
    [FromBody] V1Dtos.DeleteDevicesRequestDto requestDto,
    CancellationToken cancellationToken)
  {
    if (requestDto.DeviceIds.Count > DtoLimits.DeviceIdsMaxCount)
    {
      return BadRequest($"Too many device IDs. Maximum allowed is {DtoLimits.DeviceIdsMaxCount}.");
    }

    var candidateDevices = await appDb.Devices
      .AsNoTracking()
      .Include(x => x.DeviceGroupMembers)
      .Where(d => requestDto.DeviceIds.Contains(d.Id))
      .ToListAsync(cancellationToken);

    var principal = User.ToPrincipalDescriptor();
    if (principal is null)
    {
      return Forbid();
    }

    var candidates = candidateDevices
      .Where(device => User.CanAccessTenant(device.TenantId))
      .ToList();

    var requests = candidates
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
    for (var i = 0; i < candidates.Count; i++)
    {
      if (decisions[requests[i]].Allowed)
      {
        authorizedIdSet.Add(candidates[i].Id);
      }
    }

    var deletedCount = await appDb.Devices
      .Where(x => authorizedIdSet.Contains(x.Id))
      .ExecuteDeleteAsync(cancellationToken);

    if (deletedCount == authorizedIdSet.Count)
    {
      return new V1Dtos.DeleteManyDevicesResponseDto(
        SuccessIds: [.. authorizedIdSet],
        FailureIds: [.. requestDto.DeviceIds.Except(authorizedIdSet)]);
    }

    var remainingIds = await appDb.Devices
      .Where(x => authorizedIdSet.Contains(x.Id))
      .Select(x => x.Id)
      .ToListAsync(cancellationToken);

    var successIds = authorizedIdSet.Except(remainingIds).ToImmutableList();
    var failureIds = remainingIds.Concat(requestDto.DeviceIds.Except(authorizedIdSet)).ToImmutableList();

    return new V1Dtos.DeleteManyDevicesResponseDto(successIds, failureIds);
  }

  [HttpGet]
  public async IAsyncEnumerable<DeviceResponseDto> Get(
    [FromServices] AppDb appDb,
    [FromServices] IAgentVersionProvider agentVersionProvider,
    [EnumeratorCancellation] CancellationToken cancellationToken)
  {
    var query = await appDb.Devices
      .Include(x => x.Tags)
      .Include(x => x.Customer)
      .AsSplitQuery()
      .ApplyDeviceAccessScope(User, _deviceAccessScopeResolver, cancellationToken);

    await foreach (var device in query.AsAsyncEnumerable().WithCancellation(cancellationToken))
    {
      var isOutdated = await agentVersionProvider.IsAgentOutdated(device.AgentVersion, cancellationToken);
      yield return device.ToV1ResponseDto(isOutdated);
    }
  }

  [HttpGet("{deviceId:guid}/desktop-sessions")]
  [ProducesResponseType<V1Dtos.DesktopSessionsResponseDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  [ProducesResponseType(StatusCodes.Status409Conflict)]
  public async Task<ActionResult<V1Dtos.DesktopSessionsResponseDto>> GetDesktopSessions(
    [FromRoute] Guid deviceId,
    [FromServices] AppDb appDb,
    [FromServices] IHubContext<AgentHub, IAgentHubClient> agentHub,
    [FromServices] IDesktopSessionAccessAuthorizer desktopSessionAccessAuthorizer,
    [FromServices] IAuthorizationService authorizationService,
    [FromServices] ILogger<DevicesController> logger,
    CancellationToken cancellationToken)
  {
    var device = await appDb.Devices
      .AsNoTracking()
      .FirstOrDefaultAsync(x => x.Id == deviceId, cancellationToken);

    if (device is null)
    {
      return NotFound();
    }

    if (!User.CanAccessTenant(device.TenantId))
    {
      return NotFound();
    }

    var authResult = await authorizationService.AuthorizeAsync(User, device, DeviceResourcePolicies.Read);
    if (!authResult.Succeeded)
    {
      return NotFound();
    }

    if (!device.IsOnline || string.IsNullOrWhiteSpace(device.ConnectionId))
    {
      return Conflict("Device is currently offline.");
    }

    try
    {
      var sessions = await agentHub.Clients
        .Client(device.ConnectionId)
        .GetActiveDesktopSessions();

      var principal = User.ToPrincipalDescriptor();
      if (principal is null)
      {
        return Unauthorized();
      }

      sessions = sessions
        .Where(x => desktopSessionAccessAuthorizer.CanUse(principal, deviceId, x.SystemSessionId))
        .ToArray();

      var dtos = sessions.Select(V1Dtos.DesktopSessionResponseDto.From).ToArray();
      return Ok(new V1Dtos.DesktopSessionsResponseDto { Items = dtos });
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error while getting desktop sessions for device {DeviceId}.", deviceId);
      return Problem(
        detail: "Failed to retrieve desktop sessions from the agent.",
        statusCode: StatusCodes.Status500InternalServerError,
        title: "Agent communication failed");
    }
  }

  [HttpGet("{deviceId:guid}")]
  [ProducesResponseType<DeviceResponseDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<ActionResult<DeviceResponseDto>> GetDevice(
    [FromServices] AppDb appDb,
    [FromServices] IAgentVersionProvider agentVersionProvider,
    [FromServices] IAuthorizationService authorizationService,
    [FromRoute] Guid deviceId,
    CancellationToken cancellationToken)
  {
    var device = await appDb.Devices
      .Include(x => x.Customer)
      .FirstOrDefaultAsync(x => x.Id == deviceId, cancellationToken);
    if (device is null)
    {
      return NotFound();
    }

    if (!User.CanAccessTenant(device.TenantId))
    {
      return NotFound();
    }

    var authResult = await authorizationService.AuthorizeAsync(User, device, DeviceResourcePolicies.Read);
    if (!authResult.Succeeded)
    {
      return NotFound();
    }

    var isOutdated = await agentVersionProvider.IsAgentOutdated(device.AgentVersion, cancellationToken);
    return device.ToV1ResponseDto(isOutdated);
  }

  [HttpGet("summary")]
  public async IAsyncEnumerable<V1Dtos.DeviceSummaryDto> GetDeviceSummaries(
    [FromServices] AppDb appDb,
    [EnumeratorCancellation] CancellationToken cancellationToken)
  {
    var query = await appDb.Devices
      .ApplyDeviceAccessScope(User, _deviceAccessScopeResolver, cancellationToken);

    await foreach (var device in query.AsAsyncEnumerable().WithCancellation(cancellationToken))
    {
      yield return device.ToV1SummaryDto();
    }
  }

  [HttpPost("search")]
  public async Task<ActionResult<V1Dtos.DeviceSearchResponseDto>> SearchDevices(
    [FromBody] V1Dtos.DeviceSearchRequestDto requestDto,
    [FromServices] AppDb appDb,
    [FromServices] IAgentVersionProvider agentVersionProvider,
    [FromServices] ILogger<DevicesController> logger,
    CancellationToken cancellationToken)
  {
    var isRelationalDatabase = appDb.Database.IsRelational();
    var authorizedQuery = await appDb.Devices.AsQueryable()
      .ApplyDeviceAccessScope(User, _deviceAccessScopeResolver, cancellationToken);

    var anyDevices = await authorizedQuery.AnyAsync(cancellationToken);

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
    var filterCounts = await GetFilterCounts(scopedQuery, cancellationToken);
    var totalCount = await scopedQuery.CountAsync(cancellationToken);

    // Clamp the page so the skip multiplication cannot overflow int (which would
    // produce a negative SQL OFFSET and fail the query).
    var clampedPageSize = Math.Max(1, requestDto.PageSize);
    var clampedPage = Math.Clamp(requestDto.Page, 0, int.MaxValue / clampedPageSize);

    var devices = await scopedQuery
      .ApplySorting(requestDto.SortDefinitions)
      .Include(x => x.Tags)
      .Include(x => x.Customer)
      .AsSplitQuery()
      .Skip(clampedPage * clampedPageSize)
      .Take(clampedPageSize)
      .ToListAsync(cancellationToken);

    var pagedDtos = new List<DeviceResponseDto>(devices.Count);
    foreach (var device in devices)
    {
      var isOutdated = await agentVersionProvider.IsAgentOutdated(device.AgentVersion, cancellationToken);
      pagedDtos.Add(device.ToV1ResponseDto(isOutdated));
    }

    var response = new V1Dtos.DeviceSearchResponseDto
    {
      AnyDevicesForUser = anyDevices,
      FilterCounts = filterCounts,
      Items = pagedDtos,
      TotalItems = totalCount
    };

    return response;
  }

  [HttpPatch("{deviceId:guid}/alias")]
  [ProducesResponseType<DeviceResponseDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<ActionResult<DeviceResponseDto>> UpdateDeviceAlias(
    [FromRoute] Guid deviceId,
    [FromBody] V1Dtos.UpdateDeviceAliasRequestDto requestDto,
    [FromServices] AppDb appDb,
    [FromServices] IAgentVersionProvider agentVersionProvider,
    [FromServices] IAuthorizationService authorizationService,
    [FromServices] ILogger<DevicesController> logger,
    CancellationToken cancellationToken)
  {
    if (deviceId != requestDto.DeviceId)
    {
      return BadRequest("Device ID mismatch.");
    }

    if (requestDto.Alias is not null && requestDto.Alias.Length > 100)
    {
      return BadRequest("Alias must be 100 characters or fewer.");
    }

    var device = await appDb.Devices.FirstOrDefaultAsync(x => x.Id == deviceId, cancellationToken);
    if (device is null)
    {
      logger.LogWarning("Device {DeviceId} not found for alias update.", deviceId);
      return NotFound();
    }

    if (!User.CanAccessTenant(device.TenantId))
    {
      return NotFound();
    }

    var authResult = await authorizationService.AuthorizeAsync(User, device, DeviceResourcePolicies.AliasWrite);
    if (!authResult.Succeeded)
    {
      return NotFound();
    }

    device.Alias = requestDto.Alias ?? string.Empty;
    await appDb.SaveChangesAsync(cancellationToken);

    var isOutdated = await agentVersionProvider.IsAgentOutdated(device.AgentVersion, cancellationToken);
    return device.ToV1ResponseDto(isOutdated);
  }

  private static async Task<V1Dtos.DeviceSearchFilterCountsDto> GetFilterCounts(
    IQueryable<Device> query,
    CancellationToken cancellationToken)
  {
    return await query
      .Select(x => new { IsTagged = x.Tags!.Any(), x.IsOnline })
      .GroupBy(_ => 1)
      .OrderBy(g => g.Key)
      .Select(group => new V1Dtos.DeviceSearchFilterCountsDto
      {
        TaggedDevices = group.Count(x => x.IsTagged),
        UntaggedDevices = group.Count(x => !x.IsTagged),
        OnlineDevices = group.Count(x => x.IsOnline),
        OfflineDevices = group.Count(x => !x.IsOnline)
      })
      .FirstOrDefaultAsync(cancellationToken)
      ?? new V1Dtos.DeviceSearchFilterCountsDto();
  }
}
