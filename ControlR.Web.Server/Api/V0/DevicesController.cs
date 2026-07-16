using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Asp.Versioning;
using ControlR.Libraries.Api.Contracts.Constants;
using Microsoft.AspNetCore.Mvc;
using DeviceResponseDto = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V0.DeviceResponseDto;

namespace ControlR.Web.Server.Api.V0;

[Route(HttpConstants.V0.DevicesEndpoint)]
[ApiController]
[Authorize(Policy = RequireServerServiceAccountPolicy.PolicyName)]
[ApiVersion(ApiVersions.V0)]
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
  public async Task<ActionResult<V0Dtos.DeleteManyDevicesResponseDto>> DeleteMany(
    [FromServices] AppDb appDb,
    [FromBody] V0Dtos.DeleteDevicesRequestDto requestDto,
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
      return new V0Dtos.DeleteManyDevicesResponseDto(
        SuccessIds: [.. authorizedDeviceIds],
        FailureIds: [.. requestDto.DeviceIds.Except(authorizedIdSet)]);
    }

    var remainingIds = await appDb.Devices
      .Where(x => authorizedIdSet.Contains(x.Id))
      .Select(x => x.Id)
      .ToListAsync(cancellationToken);

    var successIds = authorizedIdSet.Except(remainingIds).ToImmutableList();
    var failureIds = remainingIds.Concat(requestDto.DeviceIds.Except(authorizedIdSet)).ToImmutableList();

    return new V0Dtos.DeleteManyDevicesResponseDto(successIds, failureIds);
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
      yield return device.ToV0ResponseDto(isOutdated);
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
    return device.ToV0ResponseDto(isOutdated);
  }

  [HttpGet("summary")]
  public async IAsyncEnumerable<V0Dtos.DeviceSummaryDto> GetDeviceSummaries(
    [FromServices] AppDb appDb,
    [EnumeratorCancellation] CancellationToken cancellationToken)
  {
    var query = appDb.Devices.OrderBy(x => x.CreatedAt);

    await foreach (var device in query.AsAsyncEnumerable().WithCancellation(cancellationToken))
    {
      yield return device.ToV0SummaryDto();
    }
  }

  [HttpPost("search")]
  public async Task<ActionResult<V0Dtos.DeviceSearchResponseDto>> SearchDevices(
    [FromBody] V0Dtos.DeviceSearchRequestDto requestDto,
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
      pagedDtos.Add(device.ToV0ResponseDto(isOutdated));
    }

    var response = new V0Dtos.DeviceSearchResponseDto
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
    [FromBody] V0Dtos.UpdateDeviceAliasRequestDto requestDto,
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
    return device.ToV0ResponseDto(isOutdated);
  }
}
