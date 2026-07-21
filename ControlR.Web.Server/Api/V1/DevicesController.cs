using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Asp.Versioning;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Hubs.Clients;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using DeviceResponseDto = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.DeviceResponseDto;

namespace ControlR.Web.Server.Api.V1;

[Route(HttpConstants.V1.DevicesEndpoint)]
[ApiController]
[Authorize(Policy = RequireServerServiceAccountPolicy.PolicyName)]
[ApiVersion(ApiVersions.V1)]
public class DevicesController() : ControllerBase
{
  [HttpDelete("{deviceId:guid}")]
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<IActionResult> DeleteDevice(
    [FromServices] AppDb appDb,
    [FromRoute] Guid deviceId,
    CancellationToken cancellationToken)
  {
    var device = await appDb.Devices.FirstOrDefaultAsync(x => x.Id == deviceId, cancellationToken);
    if (device is null)
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
    [FromBody] V1Dtos.DeleteDevicesRequestDto requestDto,
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
      return new V1Dtos.DeleteManyDevicesResponseDto(
        SuccessIds: [.. authorizedDeviceIds],
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
    var query = appDb.Devices
      .Include(x => x.Tags)
      .AsSplitQuery()
      .OrderBy(x => x.CreatedAt);

    await foreach (var device in query.AsAsyncEnumerable().WithCancellation(cancellationToken))
    {
      var isOutdated = await agentVersionProvider.IsAgentOutdated(device.AgentVersion, cancellationToken);
      yield return device.ToV1ResponseDto(isOutdated);
    }
  }

  [HttpGet("{deviceId:guid}/desktop-sessions")]
  [ProducesResponseType<IReadOnlyList<V1Dtos.DesktopSessionResponseDto>>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  [ProducesResponseType(StatusCodes.Status409Conflict)]
  public async Task<ActionResult<IReadOnlyList<V1Dtos.DesktopSessionResponseDto>>> GetDesktopSessions(
    [FromRoute] Guid deviceId,
    [FromServices] AppDb appDb,
    [FromServices] IHubContext<AgentHub, IAgentHubClient> agentHub,
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

    if (!device.IsOnline || string.IsNullOrWhiteSpace(device.ConnectionId))
    {
      return Conflict("Device is currently offline.");
    }

    try
    {
      var sessions = await agentHub.Clients
        .Client(device.ConnectionId)
        .GetActiveDesktopSessions();

      var dtos = sessions.Select(V1Dtos.DesktopSessionResponseDto.From).ToList();
      return Ok(dtos);
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
    [FromRoute] Guid deviceId,
    CancellationToken cancellationToken)
  {
    var device = await appDb.Devices.FirstOrDefaultAsync(x => x.Id == deviceId, cancellationToken);
    if (device is null)
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
    var query = appDb.Devices.OrderBy(x => x.CreatedAt);

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
    var authorizedQuery = appDb.Devices.AsQueryable();

    var filteredQuery = authorizedQuery
      .FilterBySearchText(requestDto.SearchText, isRelationalDatabase)
      .FilterByOnlineOffline(requestDto.HideOfflineDevices)
      .FilterByColumnFilters(requestDto.FilterDefinitions, isRelationalDatabase, logger);

    var scopedQuery = filteredQuery.FilterByTagIds(requestDto.TagIds, requestDto.IncludeUntaggedDevices);
    var totalCount = await scopedQuery.CountAsync(cancellationToken);

    var devices = await scopedQuery
      .ApplySorting(requestDto.SortDefinitions)
      .Include(x => x.Tags)
      .AsSplitQuery()
      .Skip(requestDto.Page * requestDto.PageSize)
      .Take(requestDto.PageSize)
      .ToListAsync(cancellationToken);

    var pagedDtos = new List<DeviceResponseDto>(devices.Count);
    foreach (var device in devices)
    {
      var isOutdated = await agentVersionProvider.IsAgentOutdated(device.AgentVersion, cancellationToken);
      pagedDtos.Add(device.ToV1ResponseDto(isOutdated));
    }

    var response = new V1Dtos.DeviceSearchResponseDto
    {
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

    device.Alias = requestDto.Alias ?? string.Empty;
    await appDb.SaveChangesAsync(cancellationToken);

    var isOutdated = await agentVersionProvider.IsAgentOutdated(device.AgentVersion, cancellationToken);
    return device.ToV1ResponseDto(isOutdated);
  }
}
