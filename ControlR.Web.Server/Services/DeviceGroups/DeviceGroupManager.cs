using ControlR.Web.Server.Authz.Permissions;
using ControlR.Web.Server.Primitives;
using ControlR.Web.Server.Services.Authorization;

namespace ControlR.Web.Server.Services.DeviceGroups;

/// <summary>
/// Manages device groups and membership. Deleting a group cascades to its
/// PermissionAssignment rows.
/// </summary>
public interface IDeviceGroupManager
{
  Task<HttpResult> AddMembers(
    Guid deviceGroupId, IReadOnlyList<Guid> deviceIds, Guid tenantId, PrincipalDescriptor actor, CancellationToken cancellationToken = default);
  Task<HttpResult<InternalDtos.DeviceGroupDetailDto>> Create(
    string name, string? description, Guid tenantId, PrincipalDescriptor actor, CancellationToken cancellationToken = default);
  Task<HttpResult> Delete(
    Guid deviceGroupId, Guid tenantId, PrincipalDescriptor actor, CancellationToken cancellationToken = default);
  Task<HttpResult<InternalDtos.DeviceGroupDetailDto>> Get(
    Guid deviceGroupId, Guid tenantId, CancellationToken cancellationToken = default);
  Task<IReadOnlyList<InternalDtos.DeviceGroupDto>> GetAll(Guid tenantId, CancellationToken cancellationToken = default);
  Task<HttpResult> RemoveMembers(
    Guid deviceGroupId, IReadOnlyList<Guid> deviceIds, Guid tenantId, PrincipalDescriptor actor, CancellationToken cancellationToken = default);
  Task<HttpResult<InternalDtos.DeviceGroupDetailDto>> Update(
    Guid deviceGroupId, string name, string? description, Guid tenantId, PrincipalDescriptor actor, CancellationToken cancellationToken = default);
}

public class DeviceGroupManager(AppDb appDb, IAuthorizationChangeLogFactory changeLogFactory) : IDeviceGroupManager
{
  private readonly AppDb _appDb = appDb;
  private readonly IAuthorizationChangeLogFactory _changeLogFactory = changeLogFactory;

  public async Task<HttpResult> AddMembers(
    Guid deviceGroupId, IReadOnlyList<Guid> deviceIds, Guid tenantId, PrincipalDescriptor actor, CancellationToken cancellationToken = default)
  {
    var group = await _appDb.DeviceGroups
      .Include(x => x.Members)
      .FirstOrDefaultAsync(x => x.Id == deviceGroupId && x.TenantId == tenantId, cancellationToken);

    if (group is null)
    {
      return HttpResult.Fail(HttpResultErrorCode.NotFound, "Device group not found.");
    }

    var existingDeviceIds = (group.Members ?? []).Select(m => m.DeviceId).ToHashSet();
    // Dedup intra-request duplicate IDs so the validity count below matches what we
    // actually insert, instead of returning a misleading "not found in tenant" BadRequest.
    var newDeviceIds = deviceIds
      .Distinct()
      .Where(id => !existingDeviceIds.Contains(id))
      .ToList();

    if (newDeviceIds.Count == 0)
    {
      return HttpResult.Ok();
    }

    // Verify all devices belong to the same tenant.
    var validDeviceCount = await _appDb.Devices
      .CountAsync(x => x.TenantId == tenantId && newDeviceIds.Contains(x.Id), cancellationToken);

    if (validDeviceCount != newDeviceIds.Count)
    {
      return HttpResult.Fail(HttpResultErrorCode.BadRequest, "One or more devices were not found in this tenant.");
    }

    foreach (var deviceId in newDeviceIds)
    {
      _appDb.DeviceGroupMembers.Add(new DeviceGroupMember
      {
        DeviceGroupId = deviceGroupId,
        DeviceId = deviceId
      });
    }

    _appDb.AuthorizationChangeLogs.Add(_changeLogFactory.Create(
      AuthorizationChangeLogActions.DeviceGroupMembersAdded,
      actor,
      AuthorizationChangeLogTargetTypes.DeviceGroup,
      deviceGroupId,
      tenantId,
      after: new DeviceGroupMembershipChange(newDeviceIds.Count)));

    await _appDb.SaveChangesAsync(cancellationToken);

    return HttpResult.Ok();
  }

  public async Task<HttpResult<InternalDtos.DeviceGroupDetailDto>> Create(
    string name, string? description, Guid tenantId, PrincipalDescriptor actor, CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrWhiteSpace(name))
    {
      return HttpResult.Fail<InternalDtos.DeviceGroupDetailDto>(HttpResultErrorCode.BadRequest, "Name is required.");
    }

    var nameConflict = await _appDb.DeviceGroups
      .AnyAsync(x => x.TenantId == tenantId && x.Name == name, cancellationToken);

    if (nameConflict)
    {
      return HttpResult.Fail<InternalDtos.DeviceGroupDetailDto>(HttpResultErrorCode.Conflict, "A device group with that name already exists.");
    }

    var group = new DeviceGroup
    {
      Name = name,
      Description = description,
      TenantId = tenantId,
      Members = []
    };

    _appDb.DeviceGroups.Add(group);

    await _appDb.ExecuteInTransaction(async () =>
    {
      await _appDb.SaveChangesAsync(cancellationToken);

      _appDb.AuthorizationChangeLogs.Add(_changeLogFactory.Create(
        AuthorizationChangeLogActions.DeviceGroupCreated,
        actor,
        AuthorizationChangeLogTargetTypes.DeviceGroup,
        group.Id,
        tenantId,
        after: new DeviceGroupSnapshot(name, description)));

      await _appDb.SaveChangesAsync(cancellationToken);
    }, cancellationToken);

    return HttpResult.Ok(MapToDetailDto(group));
  }

  public async Task<HttpResult> Delete(
    Guid deviceGroupId, Guid tenantId, PrincipalDescriptor actor, CancellationToken cancellationToken = default)
  {
    var group = await _appDb.DeviceGroups
      .Include(x => x.Members)
      .FirstOrDefaultAsync(x => x.Id == deviceGroupId && x.TenantId == tenantId, cancellationToken);

    if (group is null)
    {
      return HttpResult.Fail(HttpResultErrorCode.NotFound, "Device group not found.");
    }

    // Cascade: remove PermissionAssignment rows scoped to this device group.
    var scopeAssignments = await _appDb.PermissionAssignments
      .IgnoreQueryFilters()
      .Where(x => x.ScopeKind == PermissionScopeKind.DeviceGroup && x.ScopeId == deviceGroupId)
      .ToListAsync(cancellationToken);

    _appDb.PermissionAssignments.RemoveRange(scopeAssignments);

    _appDb.DeviceGroupMembers.RemoveRange(group.Members ?? []);

    _appDb.AuthorizationChangeLogs.Add(_changeLogFactory.Create(
      AuthorizationChangeLogActions.DeviceGroupDeleted,
      actor,
      AuthorizationChangeLogTargetTypes.DeviceGroup,
      deviceGroupId,
      tenantId,
      before: new DeviceGroupSnapshot(group.Name, group.Description)));

    _appDb.DeviceGroups.Remove(group);
    await _appDb.SaveChangesAsync(cancellationToken);

    return HttpResult.Ok();
  }

  public async Task<HttpResult<InternalDtos.DeviceGroupDetailDto>> Get(
    Guid deviceGroupId, Guid tenantId, CancellationToken cancellationToken = default)
  {
    var group = await _appDb.DeviceGroups
      .Include(x => x.Members!)
        .ThenInclude(m => m.Device)
          .ThenInclude(d => d!.Customer)
      .FirstOrDefaultAsync(x => x.Id == deviceGroupId && x.TenantId == tenantId, cancellationToken);

    if (group is null)
    {
      return HttpResult.Fail<InternalDtos.DeviceGroupDetailDto>(HttpResultErrorCode.NotFound, "Device group not found.");
    }

    return HttpResult.Ok(MapToDetailDto(group));
  }

  public async Task<IReadOnlyList<InternalDtos.DeviceGroupDto>> GetAll(Guid tenantId, CancellationToken cancellationToken = default)
  {
    var groups = await _appDb.DeviceGroups
      .Where(x => x.TenantId == tenantId)
      .Include(x => x.Members)
      .AsNoTracking()
      .OrderBy(x => x.Name)
      .ToListAsync(cancellationToken);

    return [.. groups.Select(g => new InternalDtos.DeviceGroupDto(
      g.Id, g.Name, g.Description, g.CreatedAt, g.Members?.Count ?? 0))];
  }

  public async Task<HttpResult> RemoveMembers(
    Guid deviceGroupId, IReadOnlyList<Guid> deviceIds, Guid tenantId, PrincipalDescriptor actor, CancellationToken cancellationToken = default)
  {
    var group = await _appDb.DeviceGroups
      .Include(x => x.Members)
      .FirstOrDefaultAsync(x => x.Id == deviceGroupId && x.TenantId == tenantId, cancellationToken);

    if (group is null)
    {
      return HttpResult.Fail(HttpResultErrorCode.NotFound, "Device group not found.");
    }

    var membersToRemove = (group.Members ?? [])
      .Where(m => deviceIds.Contains(m.DeviceId))
      .ToList();

    if (membersToRemove.Count == 0)
    {
      return HttpResult.Ok();
    }

    _appDb.DeviceGroupMembers.RemoveRange(membersToRemove);

    _appDb.AuthorizationChangeLogs.Add(_changeLogFactory.Create(
      AuthorizationChangeLogActions.DeviceGroupMembersRemoved,
      actor,
      AuthorizationChangeLogTargetTypes.DeviceGroup,
      deviceGroupId,
      tenantId,
      after: new DeviceGroupMembershipChange(membersToRemove.Count)));

    await _appDb.SaveChangesAsync(cancellationToken);

    return HttpResult.Ok();
  }

  public async Task<HttpResult<InternalDtos.DeviceGroupDetailDto>> Update(
    Guid deviceGroupId, string name, string? description, Guid tenantId, PrincipalDescriptor actor, CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrWhiteSpace(name))
    {
      return HttpResult.Fail<InternalDtos.DeviceGroupDetailDto>(HttpResultErrorCode.BadRequest, "Name is required.");
    }

    var group = await _appDb.DeviceGroups
      .Include(x => x.Members!)
        .ThenInclude(m => m.Device)
          .ThenInclude(d => d!.Customer)
      .FirstOrDefaultAsync(x => x.Id == deviceGroupId && x.TenantId == tenantId, cancellationToken);

    if (group is null)
    {
      return HttpResult.Fail<InternalDtos.DeviceGroupDetailDto>(HttpResultErrorCode.NotFound, "Device group not found.");
    }

    var nameConflict = await _appDb.DeviceGroups
      .AnyAsync(x => x.TenantId == tenantId && x.Name == name && x.Id != deviceGroupId, cancellationToken);

    if (nameConflict)
    {
      return HttpResult.Fail<InternalDtos.DeviceGroupDetailDto>(HttpResultErrorCode.Conflict, "A device group with that name already exists.");
    }

    var before = new DeviceGroupSnapshot(group.Name, group.Description);

    group.Name = name;
    group.Description = description;

    _appDb.AuthorizationChangeLogs.Add(_changeLogFactory.Create(
      AuthorizationChangeLogActions.DeviceGroupUpdated,
      actor,
      AuthorizationChangeLogTargetTypes.DeviceGroup,
      deviceGroupId,
      tenantId,
      before: before,
      after: new DeviceGroupSnapshot(name, description)));

    await _appDb.SaveChangesAsync(cancellationToken);

    return HttpResult.Ok(MapToDetailDto(group));
  }

  private static InternalDtos.DeviceGroupDetailDto MapToDetailDto(DeviceGroup group)
  {
    var members = (group.Members ?? [])
      .OrderBy(m => m.Device?.Name ?? string.Empty)
      .Select(m => new InternalDtos.DeviceGroupMemberDto(
        m.DeviceId,
        m.Device?.Name ?? string.Empty,
        string.IsNullOrWhiteSpace(m.Device?.Alias) ? null : m.Device?.Alias,
        m.Device?.Customer?.Name))
      .ToList();

    return new InternalDtos.DeviceGroupDetailDto(
      group.Id, group.Name, group.Description, group.CreatedAt, members);
  }
}
