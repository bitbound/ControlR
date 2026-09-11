using Asp.Versioning;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.DeviceGroups;
using ControlR.Web.Server.Authz.Permissions;
using ControlR.Web.Server.Primitives;
using ControlR.Web.Server.Services.Authorization;
using ControlR.Web.Server.Services.DeviceGroups;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ControlR.Web.Server.Api.V1;

/// <summary>
/// Device group management. Tenant scoping is enforced by the required tenantId query
/// parameter: the caller's tenant claim must match it (or the caller must be a server
/// principal), and the resolved id is passed into every manager call, whose queries all
/// carry explicit TenantId predicates. Membership mutation additionally requires the
/// group-scoped device-group.assign-devices permission evaluated against the target group.
/// </summary>
[Route(HttpConstants.V1.DeviceGroupsEndpoint)]
[ApiController]
[Authorize]
[ApiVersion(ApiVersions.V1)]
public class DeviceGroupsController(
  IDeviceGroupManager deviceGroupManager) : ControllerBase
{
  private readonly IDeviceGroupManager _deviceGroupManager = deviceGroupManager;

  [HttpPost("{deviceGroupId:guid}/members")]
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<IActionResult> AddMembers(
    [FromRoute] Guid deviceGroupId,
    [FromQuery] Guid tenantId,
    [FromBody] AddDeviceGroupMembersRequestDto request,
    [FromServices] IAuthorizationService authorizationService,
    CancellationToken cancellationToken)
  {
    if (!User.TryResolveTenantId(tenantId, out var resolvedTenantId))
    {
      return Forbid();
    }

    if (User.ToPrincipalDescriptor() is not { } actor)
    {
      return BadRequest("User ID not found.");
    }

    var authorizationResult = await authorizationService.AuthorizeAsync(
      User,
      new ResourceDescriptor(PermissionScopeKind.DeviceGroup, deviceGroupId, resolvedTenantId),
      PolicyNames.RequireDeviceGroupAssignDevices);
    if (!authorizationResult.Succeeded)
    {
      return Forbid();
    }

    var result = await _deviceGroupManager.AddMembers(
      deviceGroupId, request.DeviceIds, resolvedTenantId, actor, cancellationToken);

    if (!result.IsSuccess)
    {
      return ToV1Failure(result);
    }

    return NoContent();
  }

  [HttpPost]
  [Authorize(Policy = PolicyNames.RequireDeviceGroupsWrite)]
  [ProducesResponseType<DeviceGroupDetailDto>(StatusCodes.Status201Created)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  public async Task<ActionResult<DeviceGroupDetailDto>> Create(
    [FromQuery] Guid tenantId,
    [FromBody] CreateDeviceGroupRequestDto request,
    CancellationToken cancellationToken)
  {
    if (!User.TryResolveTenantId(tenantId, out var resolvedTenantId))
    {
      return Forbid();
    }

    if (User.ToPrincipalDescriptor() is not { } actor)
    {
      return BadRequest("User ID not found.");
    }

    var result = await _deviceGroupManager.Create(
      request.Name, request.Description, resolvedTenantId, actor, cancellationToken);

    if (!result.IsSuccess)
    {
      return ToV1Failure(result);
    }

    return CreatedAtAction(
      nameof(Get),
      new { deviceGroupId = result.Value.Id, tenantId = resolvedTenantId },
      ToV1Dto(result.Value));
  }

  [HttpDelete("{deviceGroupId:guid}")]
  [Authorize(Policy = PolicyNames.RequireDeviceGroupsWrite)]
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<IActionResult> Delete(
    [FromRoute] Guid deviceGroupId,
    [FromQuery] Guid tenantId,
    CancellationToken cancellationToken)
  {
    if (!User.TryResolveTenantId(tenantId, out var resolvedTenantId))
    {
      return Forbid();
    }

    if (User.ToPrincipalDescriptor() is not { } actor)
    {
      return BadRequest("User ID not found.");
    }

    var result = await _deviceGroupManager.Delete(deviceGroupId, resolvedTenantId, actor, cancellationToken);
    if (!result.IsSuccess)
    {
      return ToV1Failure(result);
    }

    return NoContent();
  }

  [HttpGet("{deviceGroupId:guid}")]
  [Authorize(Policy = PolicyNames.RequireDeviceGroupsRead)]
  [ProducesResponseType<DeviceGroupDetailDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<ActionResult<DeviceGroupDetailDto>> Get(
    [FromRoute] Guid deviceGroupId,
    [FromQuery] Guid tenantId,
    CancellationToken cancellationToken)
  {
    if (!User.TryResolveTenantId(tenantId, out var resolvedTenantId))
    {
      return Forbid();
    }

    var result = await _deviceGroupManager.Get(deviceGroupId, resolvedTenantId, cancellationToken);
    if (!result.IsSuccess)
    {
      return ToV1Failure(result);
    }

    return Ok(ToV1Dto(result.Value));
  }

  [HttpGet]
  [Authorize(Policy = PolicyNames.RequireDeviceGroupsRead)]
  [ProducesResponseType<DeviceGroupsResponseDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  public async Task<ActionResult<DeviceGroupsResponseDto>> GetAll(
    [FromQuery] Guid tenantId,
    CancellationToken cancellationToken)
  {
    if (!User.TryResolveTenantId(tenantId, out var resolvedTenantId))
    {
      return Forbid();
    }

    var groups = await _deviceGroupManager.GetAll(resolvedTenantId, cancellationToken);

    return Ok(new DeviceGroupsResponseDto
    {
      Items = [.. groups.Select(ToV1Dto)]
    });
  }

  [HttpDelete("{deviceGroupId:guid}/members")]
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<IActionResult> RemoveMembers(
    [FromRoute] Guid deviceGroupId,
    [FromQuery] Guid tenantId,
    [FromBody] RemoveDeviceGroupMembersRequestDto request,
    [FromServices] IAuthorizationService authorizationService,
    CancellationToken cancellationToken)
  {
    if (!User.TryResolveTenantId(tenantId, out var resolvedTenantId))
    {
      return Forbid();
    }

    if (User.ToPrincipalDescriptor() is not { } actor)
    {
      return BadRequest("User ID not found.");
    }

    var authorizationResult = await authorizationService.AuthorizeAsync(
      User,
      new ResourceDescriptor(PermissionScopeKind.DeviceGroup, deviceGroupId, resolvedTenantId),
      PolicyNames.RequireDeviceGroupAssignDevices);
    if (!authorizationResult.Succeeded)
    {
      return Forbid();
    }

    var result = await _deviceGroupManager.RemoveMembers(
      deviceGroupId, request.DeviceIds, resolvedTenantId, actor, cancellationToken);

    if (!result.IsSuccess)
    {
      return ToV1Failure(result);
    }

    return NoContent();
  }

  [HttpPut("{deviceGroupId:guid}")]
  [Authorize(Policy = PolicyNames.RequireDeviceGroupsWrite)]
  [ProducesResponseType<DeviceGroupDetailDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<ActionResult<DeviceGroupDetailDto>> Update(
    [FromRoute] Guid deviceGroupId,
    [FromQuery] Guid tenantId,
    [FromBody] UpdateDeviceGroupRequestDto request,
    CancellationToken cancellationToken)
  {
    if (!User.TryResolveTenantId(tenantId, out var resolvedTenantId))
    {
      return Forbid();
    }

    if (User.ToPrincipalDescriptor() is not { } actor)
    {
      return BadRequest("User ID not found.");
    }

    var result = await _deviceGroupManager.Update(
      deviceGroupId, request.Name, request.Description, resolvedTenantId, actor, cancellationToken);

    if (!result.IsSuccess)
    {
      return ToV1Failure(result);
    }

    return Ok(ToV1Dto(result.Value));
  }

  private static DeviceGroupDto ToV1Dto(InternalDtos.DeviceGroupDto source)
  {
    return new DeviceGroupDto(
      source.Id,
      source.Name,
      source.Description,
      source.CreatedAt,
      source.MemberCount);
  }

  private static DeviceGroupDetailDto ToV1Dto(InternalDtos.DeviceGroupDetailDto source)
  {
    return new DeviceGroupDetailDto(
      source.Id,
      source.Name,
      source.Description,
      source.CreatedAt,
      [.. source.Members.Select(m => new DeviceGroupMemberDto(
        m.DeviceId,
        m.DeviceName,
        m.Alias,
        m.CustomerName))]);
  }

  // The manager never returns Forbidden today (cross-tenant ids surface as NotFound via the
  // explicit TenantId predicates), but keep the collapse so a future Forbidden cannot act as
  // an existence oracle against other tenants' groups.
  private static ActionResult ToV1Failure<T>(HttpResult<T> result) =>
    ToV1Failure(result.ToHttpResult());

  private static ActionResult ToV1Failure(HttpResult result)
  {
    return result.ErrorCode == HttpResultErrorCode.Forbidden
      ? new NotFoundResult()
      : result.ToActionResult();
  }
}