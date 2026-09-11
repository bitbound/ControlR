using Asp.Versioning;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.UserGroups;
using ControlR.Web.Server.Authz.Permissions;
using ControlR.Web.Server.Primitives;
using ControlR.Web.Server.Services.Authorization;
using ControlR.Web.Server.Services.UserGroups;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ControlR.Web.Server.Api.V1;

/// <summary>
/// User group management. Tenant scoping is enforced by the required tenantId query
/// parameter: the caller's tenant claim must match it (or the caller must be a server
/// principal), and the resolved id is passed into every manager call, whose queries all
/// carry explicit TenantId predicates. Membership mutation additionally requires the
/// group-scoped user-group.assign-users permission evaluated against the target group.
/// </summary>
[Route(HttpConstants.V1.UserGroupsEndpoint)]
[ApiController]
[Authorize]
[ApiVersion(ApiVersions.V1)]
public class UserGroupsController(
  IUserGroupManager userGroupManager) : ControllerBase
{
  private readonly IUserGroupManager _userGroupManager = userGroupManager;

  [HttpPost("{userGroupId:guid}/members")]
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<IActionResult> AddMembers(
    [FromRoute] Guid userGroupId,
    [FromQuery] Guid tenantId,
    [FromBody] AddUserGroupMembersRequestDto request,
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
      new ResourceDescriptor(PermissionScopeKind.UserGroup, userGroupId, resolvedTenantId),
      PolicyNames.RequireUserGroupAssignUsers);
    if (!authorizationResult.Succeeded)
    {
      return Forbid();
    }

    var result = await _userGroupManager.AddMembers(
      userGroupId, request.UserIds, resolvedTenantId, actor, cancellationToken);

    if (!result.IsSuccess)
    {
      return ToV1Failure(result);
    }

    return NoContent();
  }

  [HttpPost]
  [Authorize(Policy = PolicyNames.RequireUserGroupsWrite)]
  [ProducesResponseType<UserGroupDetailDto>(StatusCodes.Status201Created)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  public async Task<ActionResult<UserGroupDetailDto>> Create(
    [FromQuery] Guid tenantId,
    [FromBody] CreateUserGroupRequestDto request,
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

    var result = await _userGroupManager.Create(
      request.Name, request.Description, resolvedTenantId, actor, cancellationToken);

    if (!result.IsSuccess)
    {
      return ToV1Failure(result);
    }

    return CreatedAtAction(
      nameof(Get),
      new { userGroupId = result.Value.Id, tenantId = resolvedTenantId },
      ToV1Dto(result.Value));
  }

  [HttpDelete("{userGroupId:guid}")]
  [Authorize(Policy = PolicyNames.RequireUserGroupsWrite)]
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<IActionResult> Delete(
    [FromRoute] Guid userGroupId,
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

    var result = await _userGroupManager.Delete(userGroupId, resolvedTenantId, actor, cancellationToken);
    if (!result.IsSuccess)
    {
      return ToV1Failure(result);
    }

    return NoContent();
  }

  [HttpGet("{userGroupId:guid}")]
  [Authorize(Policy = PolicyNames.RequireUserGroupsRead)]
  [ProducesResponseType<UserGroupDetailDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<ActionResult<UserGroupDetailDto>> Get(
    [FromRoute] Guid userGroupId,
    [FromQuery] Guid tenantId,
    CancellationToken cancellationToken)
  {
    if (!User.TryResolveTenantId(tenantId, out var resolvedTenantId))
    {
      return Forbid();
    }

    var result = await _userGroupManager.Get(userGroupId, resolvedTenantId, cancellationToken);
    if (!result.IsSuccess)
    {
      return ToV1Failure(result);
    }

    return Ok(ToV1Dto(result.Value));
  }

  [HttpGet]
  [Authorize(Policy = PolicyNames.RequireUserGroupsRead)]
  [ProducesResponseType<UserGroupsResponseDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  public async Task<ActionResult<UserGroupsResponseDto>> GetAll(
    [FromQuery] Guid tenantId,
    CancellationToken cancellationToken)
  {
    if (!User.TryResolveTenantId(tenantId, out var resolvedTenantId))
    {
      return Forbid();
    }

    var groups = await _userGroupManager.GetAll(resolvedTenantId, cancellationToken);

    return Ok(new UserGroupsResponseDto
    {
      Items = [.. groups.Select(ToV1Dto)]
    });
  }

  [HttpDelete("{userGroupId:guid}/members")]
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<IActionResult> RemoveMembers(
    [FromRoute] Guid userGroupId,
    [FromQuery] Guid tenantId,
    [FromBody] RemoveUserGroupMembersRequestDto request,
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
      new ResourceDescriptor(PermissionScopeKind.UserGroup, userGroupId, resolvedTenantId),
      PolicyNames.RequireUserGroupAssignUsers);
    if (!authorizationResult.Succeeded)
    {
      return Forbid();
    }

    var result = await _userGroupManager.RemoveMembers(
      userGroupId, request.UserIds, resolvedTenantId, actor, cancellationToken);

    if (!result.IsSuccess)
    {
      return ToV1Failure(result);
    }

    return NoContent();
  }

  [HttpPut("{userGroupId:guid}")]
  [Authorize(Policy = PolicyNames.RequireUserGroupsWrite)]
  [ProducesResponseType<UserGroupDetailDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<ActionResult<UserGroupDetailDto>> Update(
    [FromRoute] Guid userGroupId,
    [FromQuery] Guid tenantId,
    [FromBody] UpdateUserGroupRequestDto request,
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

    var result = await _userGroupManager.Update(
      userGroupId, request.Name, request.Description, resolvedTenantId, actor, cancellationToken);

    if (!result.IsSuccess)
    {
      return ToV1Failure(result);
    }

    return Ok(ToV1Dto(result.Value));
  }

  private static UserGroupDto ToV1Dto(InternalDtos.UserGroupDto source)
  {
    return new UserGroupDto(
      source.Id,
      source.Name,
      source.Description,
      source.CreatedAt,
      source.MemberCount);
  }

  private static UserGroupDetailDto ToV1Dto(InternalDtos.UserGroupDetailDto source)
  {
    return new UserGroupDetailDto(
      source.Id,
      source.Name,
      source.Description,
      source.CreatedAt,
      [.. source.Members.Select(m => new UserGroupMemberDto(
        m.UserId,
        m.UserName,
        m.DisplayName,
        m.LastLogin))]);
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