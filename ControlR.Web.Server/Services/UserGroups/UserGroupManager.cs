using ControlR.Web.Server.Authz.Permissions;
using ControlR.Web.Server.Primitives;
using ControlR.Web.Server.Services.Authorization;

namespace ControlR.Web.Server.Services.UserGroups;

/// <summary>
/// Manages user groups and membership. Deleting a group cascades to its
/// PermissionAssignment rows.
/// </summary>
public interface IUserGroupManager
{
  Task<HttpResult> AddMembers(
    Guid userGroupId, IReadOnlyList<Guid> userIds, Guid tenantId, PrincipalDescriptor actor, CancellationToken cancellationToken = default);

  Task<HttpResult<InternalDtos.UserGroupDetailDto>> Create(
    string name, string? description, Guid tenantId, PrincipalDescriptor actor, CancellationToken cancellationToken = default);

  Task<HttpResult> Delete(
    Guid userGroupId, Guid tenantId, PrincipalDescriptor actor, CancellationToken cancellationToken = default);

  Task<HttpResult<InternalDtos.UserGroupDetailDto>> Get(
    Guid userGroupId, Guid tenantId, CancellationToken cancellationToken = default);

  Task<IReadOnlyList<InternalDtos.UserGroupDto>> GetAll(Guid tenantId, CancellationToken cancellationToken = default);

  Task<HttpResult> RemoveMembers(
    Guid userGroupId, IReadOnlyList<Guid> userIds, Guid tenantId, PrincipalDescriptor actor, CancellationToken cancellationToken = default);

  Task<HttpResult<InternalDtos.UserGroupDetailDto>> Update(
    Guid userGroupId, string name, string? description, Guid tenantId, PrincipalDescriptor actor, CancellationToken cancellationToken = default);
}

public class UserGroupManager(AppDb appDb, IAuthorizationChangeLogFactory changeLogFactory) : IUserGroupManager
{
  private readonly AppDb _appDb = appDb;
  private readonly IAuthorizationChangeLogFactory _changeLogFactory = changeLogFactory;

  public async Task<HttpResult> AddMembers(
    Guid userGroupId, IReadOnlyList<Guid> userIds, Guid tenantId, PrincipalDescriptor actor, CancellationToken cancellationToken = default)
  {
    var group = await _appDb.UserGroups
      .Include(x => x.Members)
      .FirstOrDefaultAsync(x => x.Id == userGroupId && x.TenantId == tenantId, cancellationToken);

    if (group is null)
    {
      return HttpResult.Fail(HttpResultErrorCode.NotFound, "User group not found.");
    }

    var existingUserIds = (group.Members ?? []).Select(m => m.UserId).ToHashSet();
    // Dedup intra-request duplicate IDs so the validity count below matches what we
    // actually insert, instead of returning a misleading "not found in tenant" BadRequest.
    var newUserIds = userIds
      .Distinct()
      .Where(id => !existingUserIds.Contains(id))
      .ToList();

    if (newUserIds.Count == 0)
    {
      return HttpResult.Ok();
    }

    var validUserCount = await _appDb.Users
      .CountAsync(x => x.TenantId == tenantId && newUserIds.Contains(x.Id), cancellationToken);

    if (validUserCount != newUserIds.Count)
    {
      return HttpResult.Fail(HttpResultErrorCode.BadRequest, "One or more users were not found in this tenant.");
    }

    foreach (var userId in newUserIds)
    {
      _appDb.UserGroupMembers.Add(new UserGroupMember
      {
        UserGroupId = userGroupId,
        UserId = userId
      });
    }

    _appDb.AuthorizationChangeLogs.Add(_changeLogFactory.Create(
      AuthorizationChangeLogActions.UserGroupMembersAdded,
      actor,
      AuthorizationChangeLogTargetTypes.UserGroup,
      userGroupId,
      tenantId,
      after: new UserGroupMembershipChange(newUserIds.Count)));

    await _appDb.SaveChangesAsync(cancellationToken);

    return HttpResult.Ok();
  }

  public async Task<HttpResult<InternalDtos.UserGroupDetailDto>> Create(
    string name, string? description, Guid tenantId, PrincipalDescriptor actor, CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrWhiteSpace(name))
    {
      return HttpResult.Fail<InternalDtos.UserGroupDetailDto>(HttpResultErrorCode.BadRequest, "Name is required.");
    }

    var nameConflict = await _appDb.UserGroups
      .AnyAsync(x => x.TenantId == tenantId && x.Name == name, cancellationToken);

    if (nameConflict)
    {
      return HttpResult.Fail<InternalDtos.UserGroupDetailDto>(HttpResultErrorCode.Conflict, "A user group with that name already exists.");
    }

    var group = new UserGroup
    {
      Name = name,
      Description = description,
      TenantId = tenantId,
      Members = []
    };

    _appDb.UserGroups.Add(group);

    await _appDb.SaveChangesAsync(cancellationToken);

    _appDb.AuthorizationChangeLogs.Add(_changeLogFactory.Create(
      AuthorizationChangeLogActions.UserGroupCreated,
      actor,
      AuthorizationChangeLogTargetTypes.UserGroup,
      group.Id,
      tenantId,
      after: new UserGroupSnapshot(name, description)));

    await _appDb.SaveChangesAsync(cancellationToken);

    return HttpResult.Ok(await MapToDetailDto(group, cancellationToken));
  }

  public async Task<HttpResult> Delete(
    Guid userGroupId, Guid tenantId, PrincipalDescriptor actor, CancellationToken cancellationToken = default)
  {
    var group = await _appDb.UserGroups
      .Include(x => x.Members)
      .FirstOrDefaultAsync(x => x.Id == userGroupId && x.TenantId == tenantId, cancellationToken);

    if (group is null)
    {
      return HttpResult.Fail(HttpResultErrorCode.NotFound, "User group not found.");
    }

    // Cascade: remove PermissionAssignment rows where this user group is the principal.
    var principalAssignments = await _appDb.PermissionAssignments
      .IgnoreQueryFilters()
      .Where(x => x.PrincipalKind == PermissionPrincipalKind.UserGroup && x.PrincipalId == userGroupId)
      .ToListAsync(cancellationToken);

    _appDb.PermissionAssignments.RemoveRange(principalAssignments);

    _appDb.UserGroupMembers.RemoveRange(group.Members ?? []);

    _appDb.AuthorizationChangeLogs.Add(_changeLogFactory.Create(
      AuthorizationChangeLogActions.UserGroupDeleted,
      actor,
      AuthorizationChangeLogTargetTypes.UserGroup,
      userGroupId,
      tenantId,
      before: new UserGroupSnapshot(group.Name, group.Description)));

    _appDb.UserGroups.Remove(group);
    await _appDb.SaveChangesAsync(cancellationToken);

    return HttpResult.Ok();
  }

  public async Task<HttpResult<InternalDtos.UserGroupDetailDto>> Get(
    Guid userGroupId, Guid tenantId, CancellationToken cancellationToken = default)
  {
    var group = await _appDb.UserGroups
      .Include(x => x.Members!)
        .ThenInclude(m => m.User)
      .FirstOrDefaultAsync(x => x.Id == userGroupId && x.TenantId == tenantId, cancellationToken);

    if (group is null)
    {
      return HttpResult.Fail<InternalDtos.UserGroupDetailDto>(HttpResultErrorCode.NotFound, "User group not found.");
    }

    return HttpResult.Ok(await MapToDetailDto(group, cancellationToken));
  }

  public async Task<IReadOnlyList<InternalDtos.UserGroupDto>> GetAll(Guid tenantId, CancellationToken cancellationToken = default)
  {
    var groups = await _appDb.UserGroups
      .Where(x => x.TenantId == tenantId)
      .Include(x => x.Members)
      .AsNoTracking()
      .OrderBy(x => x.Name)
      .ToListAsync(cancellationToken);

    return [.. groups.Select(g => new InternalDtos.UserGroupDto(
      g.Id, g.Name, g.Description, g.CreatedAt, g.Members?.Count ?? 0))];
  }

  public async Task<HttpResult> RemoveMembers(
    Guid userGroupId, IReadOnlyList<Guid> userIds, Guid tenantId, PrincipalDescriptor actor, CancellationToken cancellationToken = default)
  {
    var group = await _appDb.UserGroups
      .Include(x => x.Members)
      .FirstOrDefaultAsync(x => x.Id == userGroupId && x.TenantId == tenantId, cancellationToken);

    if (group is null)
    {
      return HttpResult.Fail(HttpResultErrorCode.NotFound, "User group not found.");
    }

    var membersToRemove = (group.Members ?? [])
      .Where(m => userIds.Contains(m.UserId))
      .ToList();

    if (membersToRemove.Count == 0)
    {
      return HttpResult.Ok();
    }

    _appDb.UserGroupMembers.RemoveRange(membersToRemove);

    _appDb.AuthorizationChangeLogs.Add(_changeLogFactory.Create(
      AuthorizationChangeLogActions.UserGroupMembersRemoved,
      actor,
      AuthorizationChangeLogTargetTypes.UserGroup,
      userGroupId,
      tenantId,
      after: new UserGroupMembershipChange(membersToRemove.Count)));

    await _appDb.SaveChangesAsync(cancellationToken);

    return HttpResult.Ok();
  }

  public async Task<HttpResult<InternalDtos.UserGroupDetailDto>> Update(
    Guid userGroupId, string name, string? description, Guid tenantId, PrincipalDescriptor actor, CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrWhiteSpace(name))
    {
      return HttpResult.Fail<InternalDtos.UserGroupDetailDto>(HttpResultErrorCode.BadRequest, "Name is required.");
    }

    var group = await _appDb.UserGroups
      .Include(x => x.Members!)
        .ThenInclude(m => m.User)
      .FirstOrDefaultAsync(x => x.Id == userGroupId && x.TenantId == tenantId, cancellationToken);

    if (group is null)
    {
      return HttpResult.Fail<InternalDtos.UserGroupDetailDto>(HttpResultErrorCode.NotFound, "User group not found.");
    }

    var nameConflict = await _appDb.UserGroups
      .AnyAsync(x => x.TenantId == tenantId && x.Name == name && x.Id != userGroupId, cancellationToken);

    if (nameConflict)
    {
      return HttpResult.Fail<InternalDtos.UserGroupDetailDto>(HttpResultErrorCode.Conflict, "A user group with that name already exists.");
    }

    var before = new UserGroupSnapshot(group.Name, group.Description);

    group.Name = name;
    group.Description = description;

    _appDb.AuthorizationChangeLogs.Add(_changeLogFactory.Create(
      AuthorizationChangeLogActions.UserGroupUpdated,
      actor,
      AuthorizationChangeLogTargetTypes.UserGroup,
      userGroupId,
      tenantId,
      before: before,
      after: new UserGroupSnapshot(name, description)));

    await _appDb.SaveChangesAsync(cancellationToken);

    return HttpResult.Ok(await MapToDetailDto(group, cancellationToken));
  }

  private async Task<InternalDtos.UserGroupDetailDto> MapToDetailDto(UserGroup group, CancellationToken cancellationToken)
  {
    var members = (group.Members ?? []).OrderBy(m => m.User?.UserName ?? string.Empty).ToList();

    var memberIds = members.Select(m => m.UserId).ToList();

    var displayNames = await _appDb.UserPreferences
      .Where(x => memberIds.Contains(x.UserId) && x.Name == UserPreferenceNames.UserDisplayName)
      .Select(x => new { x.UserId, x.Value })
      .ToListAsync(cancellationToken);

    var displayNamesLookup = displayNames.ToDictionary(x => x.UserId, x => x.Value);

    var memberDtos = members
      .Select(m => new InternalDtos.UserGroupMemberDto(
        m.UserId,
        m.User?.UserName ?? string.Empty,
        displayNamesLookup.GetValueOrDefault(m.UserId),
        m.User?.LastLogin))
      .ToList();

    return new InternalDtos.UserGroupDetailDto(
      group.Id, group.Name, group.Description, group.CreatedAt, memberDtos);
  }
}
