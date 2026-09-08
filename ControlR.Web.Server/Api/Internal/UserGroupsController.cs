using ControlR.Web.Server.Authz.Permissions;
using Microsoft.AspNetCore.Mvc;

namespace ControlR.Web.Server.Api.Internal;

[Route(HttpConstants.Internal.UserGroupsEndpoint)]
[ApiController]
[Authorize]
[EndpointGroupName(OpenApiConstants.InternalGroupName)]
public class UserGroupsController(IUserGroupManager userGroupManager) : ControllerBase
{
  private readonly IUserGroupManager _userGroupManager = userGroupManager;

  [HttpPost("{userGroupId:guid}/members")]
  public async Task<IActionResult> AddMembers(
    [FromRoute] Guid userGroupId,
    [FromBody] InternalDtos.AddUserGroupMembersRequestDto request,
    [FromServices] IAuthorizationService authorizationService,
    CancellationToken cancellationToken)
  {
    if (!User.TryGetTenantId(out var tenantId))
    {
      return BadRequest("User tenant not found.");
    }

    var authorizationResult = await authorizationService.AuthorizeAsync(
      User,
      new ResourceDescriptor(PermissionScopeKind.UserGroup, userGroupId, tenantId),
      PolicyNames.RequireUserGroupAssignUsers);
    if (!authorizationResult.Succeeded)
    {
      return Forbid();
    }

    if (User.ToPrincipalDescriptor() is not { } actor)
    {
      return BadRequest("User ID not found.");
    }

    var result = await _userGroupManager.AddMembers(
      userGroupId, request.UserIds, tenantId, actor, cancellationToken);

    if (!result.IsSuccess)
    {
      return result.ToActionResult();
    }

    return NoContent();
  }

  [HttpPost]
  [Authorize(Policy = PolicyNames.RequireUserGroupsWrite)]
  public async Task<ActionResult<InternalDtos.UserGroupDetailDto>> Create(
    [FromBody] InternalDtos.CreateUserGroupRequestDto request,
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

    var result = await _userGroupManager.Create(
      request.Name, request.Description, tenantId, actor, cancellationToken);

    if (!result.IsSuccess)
    {
      return result.ToHttpResult().ToActionResult();
    }

    return Ok(result.Value);
  }

  [HttpDelete("{userGroupId:guid}")]
  [Authorize(Policy = PolicyNames.RequireUserGroupsWrite)]
  public async Task<IActionResult> Delete(
    [FromRoute] Guid userGroupId,
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

    var result = await _userGroupManager.Delete(userGroupId, tenantId, actor, cancellationToken);
    if (!result.IsSuccess)
    {
      return result.ToActionResult();
    }

    return NoContent();
  }

  [HttpGet("{userGroupId:guid}")]
  [Authorize(Policy = PolicyNames.RequireUserGroupsRead)]
  public async Task<ActionResult<InternalDtos.UserGroupDetailDto>> Get(
    [FromRoute] Guid userGroupId,
    CancellationToken cancellationToken)
  {
    if (!User.TryGetTenantId(out var tenantId))
    {
      return BadRequest("User tenant not found.");
    }

    var result = await _userGroupManager.Get(userGroupId, tenantId, cancellationToken);
    if (!result.IsSuccess)
    {
      return result.ToHttpResult().ToActionResult();
    }

    return Ok(result.Value);
  }

  [HttpGet]
  [Authorize(Policy = PolicyNames.RequireUserGroupsRead)]
  public async Task<ActionResult<IReadOnlyList<InternalDtos.UserGroupDto>>> GetAll(
    CancellationToken cancellationToken)
  {
    if (!User.TryGetTenantId(out var tenantId))
    {
      return BadRequest("User tenant not found.");
    }

    var groups = await _userGroupManager.GetAll(tenantId, cancellationToken);
    return Ok(groups);
  }

  [HttpDelete("{userGroupId:guid}/members")]
  public async Task<IActionResult> RemoveMembers(
    [FromRoute] Guid userGroupId,
    [FromBody] InternalDtos.RemoveUserGroupMembersRequestDto request,
    [FromServices] IAuthorizationService authorizationService,
    CancellationToken cancellationToken)
  {
    if (!User.TryGetTenantId(out var tenantId))
    {
      return BadRequest("User tenant not found.");
    }

    var authorizationResult = await authorizationService.AuthorizeAsync(
      User,
      new ResourceDescriptor(PermissionScopeKind.UserGroup, userGroupId, tenantId),
      PolicyNames.RequireUserGroupAssignUsers);
    if (!authorizationResult.Succeeded)
    {
      return Forbid();
    }

    if (User.ToPrincipalDescriptor() is not { } actor)
    {
      return BadRequest("User ID not found.");
    }

    var result = await _userGroupManager.RemoveMembers(
      userGroupId, request.UserIds, tenantId, actor, cancellationToken);

    if (!result.IsSuccess)
    {
      return result.ToActionResult();
    }

    return NoContent();
  }

  [HttpPut("{userGroupId:guid}")]
  [Authorize(Policy = PolicyNames.RequireUserGroupsWrite)]
  public async Task<ActionResult<InternalDtos.UserGroupDetailDto>> Update(
    [FromRoute] Guid userGroupId,
    [FromBody] InternalDtos.UpdateUserGroupRequestDto request,
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

    var result = await _userGroupManager.Update(
      userGroupId, request.Name, request.Description, tenantId, actor, cancellationToken);

    if (!result.IsSuccess)
    {
      return result.ToHttpResult().ToActionResult();
    }

    return Ok(result.Value);
  }
}
