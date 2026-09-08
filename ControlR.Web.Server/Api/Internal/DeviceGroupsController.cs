using ControlR.Web.Server.Authz.Permissions;
using Microsoft.AspNetCore.Mvc;

namespace ControlR.Web.Server.Api.Internal;

[Route(HttpConstants.Internal.DeviceGroupsEndpoint)]
[ApiController]
[Authorize]
[EndpointGroupName(OpenApiConstants.InternalGroupName)]
public class DeviceGroupsController(IDeviceGroupManager deviceGroupManager) : ControllerBase
{
  private readonly IDeviceGroupManager _deviceGroupManager = deviceGroupManager;

  [HttpPost("{deviceGroupId:guid}/members")]
  public async Task<IActionResult> AddMembers(
    [FromRoute] Guid deviceGroupId,
    [FromBody] InternalDtos.AddDeviceGroupMembersRequestDto request,
    [FromServices] IAuthorizationService authorizationService,
    CancellationToken cancellationToken)
  {
    if (!User.TryGetTenantId(out var tenantId))
    {
      return BadRequest("User tenant not found.");
    }

    var authorizationResult = await authorizationService.AuthorizeAsync(
      User,
      new ResourceDescriptor(PermissionScopeKind.DeviceGroup, deviceGroupId, tenantId),
      PolicyNames.RequireDeviceGroupAssignDevices);
    if (!authorizationResult.Succeeded)
    {
      return Forbid();
    }

    if (User.ToPrincipalDescriptor() is not { } actor)
    {
      return BadRequest("User ID not found.");
    }

    var result = await _deviceGroupManager.AddMembers(
      deviceGroupId, request.DeviceIds, tenantId, actor, cancellationToken);

    if (!result.IsSuccess)
    {
      return result.ToActionResult();
    }

    return NoContent();
  }

  [HttpPost]
  [Authorize(Policy = PolicyNames.RequireDeviceGroupsWrite)]
  public async Task<ActionResult<InternalDtos.DeviceGroupDetailDto>> Create(
    [FromBody] InternalDtos.CreateDeviceGroupRequestDto request,
    CancellationToken cancellationToken)
  {
    if (!User.TryGetTenantId(out var tenantId))
    {
      return BadRequest("User tenant not found.");
    }

    if (User.ToPrincipalDescriptor() is not { } actor)
    {
      return BadRequest("User ID not found.");
    }

    var result = await _deviceGroupManager.Create(
      request.Name, request.Description, tenantId, actor, cancellationToken);

    if (!result.IsSuccess)
    {
      return result.ToHttpResult().ToActionResult();
    }

    return Ok(result.Value);
  }

  [HttpDelete("{deviceGroupId:guid}")]
  [Authorize(Policy = PolicyNames.RequireDeviceGroupsWrite)]
  public async Task<IActionResult> Delete(
    [FromRoute] Guid deviceGroupId,
    CancellationToken cancellationToken)
  {
    if (!User.TryGetTenantId(out var tenantId))
    {
      return BadRequest("User tenant not found.");
    }

    if (User.ToPrincipalDescriptor() is not { } actor)
    {
      return BadRequest("User ID not found.");
    }

    var result = await _deviceGroupManager.Delete(deviceGroupId, tenantId, actor, cancellationToken);
    if (!result.IsSuccess)
    {
      return result.ToActionResult();
    }

    return NoContent();
  }

  [HttpGet("{deviceGroupId:guid}")]
  [Authorize(Policy = PolicyNames.RequireDeviceGroupsRead)]
  public async Task<ActionResult<InternalDtos.DeviceGroupDetailDto>> Get(
    [FromRoute] Guid deviceGroupId,
    CancellationToken cancellationToken)
  {
    if (!User.TryGetTenantId(out var tenantId))
    {
      return BadRequest("User tenant not found.");
    }

    var result = await _deviceGroupManager.Get(deviceGroupId, tenantId, cancellationToken);
    if (!result.IsSuccess)
    {
      return result.ToHttpResult().ToActionResult();
    }

    return Ok(result.Value);
  }

  [HttpGet]
  [Authorize(Policy = PolicyNames.RequireDeviceGroupsRead)]
  public async Task<ActionResult<IReadOnlyList<InternalDtos.DeviceGroupDto>>> GetAll(
    CancellationToken cancellationToken)
  {
    if (!User.TryGetTenantId(out var tenantId))
    {
      return BadRequest("User tenant not found.");
    }

    var groups = await _deviceGroupManager.GetAll(tenantId, cancellationToken);
    return Ok(groups);
  }

  [HttpDelete("{deviceGroupId:guid}/members")]
  public async Task<IActionResult> RemoveMembers(
    [FromRoute] Guid deviceGroupId,
    [FromBody] InternalDtos.RemoveDeviceGroupMembersRequestDto request,
    [FromServices] IAuthorizationService authorizationService,
    CancellationToken cancellationToken)
  {
    if (!User.TryGetTenantId(out var tenantId))
    {
      return BadRequest("User tenant not found.");
    }

    var authorizationResult = await authorizationService.AuthorizeAsync(
      User,
      new ResourceDescriptor(PermissionScopeKind.DeviceGroup, deviceGroupId, tenantId),
      PolicyNames.RequireDeviceGroupAssignDevices);
    if (!authorizationResult.Succeeded)
    {
      return Forbid();
    }

    if (User.ToPrincipalDescriptor() is not { } actor)
    {
      return BadRequest("User ID not found.");
    }

    var result = await _deviceGroupManager.RemoveMembers(
      deviceGroupId, request.DeviceIds, tenantId, actor, cancellationToken);

    if (!result.IsSuccess)
    {
      return result.ToActionResult();
    }

    return NoContent();
  }

  [HttpPut("{deviceGroupId:guid}")]
  [Authorize(Policy = PolicyNames.RequireDeviceGroupsWrite)]
  public async Task<ActionResult<InternalDtos.DeviceGroupDetailDto>> Update(
    [FromRoute] Guid deviceGroupId,
    [FromBody] InternalDtos.UpdateDeviceGroupRequestDto request,
    CancellationToken cancellationToken)
  {
    if (!User.TryGetTenantId(out var tenantId))
    {
      return BadRequest("User tenant not found.");
    }

    if (User.ToPrincipalDescriptor() is not { } actor)
    {
      return BadRequest("User ID not found.");
    }

    var result = await _deviceGroupManager.Update(
      deviceGroupId, request.Name, request.Description, tenantId, actor, cancellationToken);

    if (!result.IsSuccess)
    {
      return result.ToHttpResult().ToActionResult();
    }

    return Ok(result.Value);
  }
}
