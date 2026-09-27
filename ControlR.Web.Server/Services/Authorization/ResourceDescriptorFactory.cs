using ControlR.Web.Server.Authz.Permissions;

namespace ControlR.Web.Server.Services.Authorization;

/// <summary>
/// Builds the <see cref="ResourceDescriptor"/> that an endpoint evaluates a permission against.
/// </summary>
public interface IResourceDescriptorFactory
{
  /// <summary>
  /// Builds a device descriptor, populating the customer and device group ids so customer-scoped
  /// and group-scoped grants can reach the device. Uses the device's loaded group members when
  /// present. Otherwise it loads them from the database.
  /// </summary>
  Task<ResourceDescriptor> CreateDevice(Device device, CancellationToken cancellationToken = default);

  /// <summary>
  /// Builds a descriptor for an assignment scope. Returns null when the scope kind or id does not
  /// resolve to a row owned by the given tenant.
  /// </summary>
  Task<ResourceDescriptor?> CreateScope(
    PermissionScopeKind scopeKind,
    Guid? scopeId,
    Guid tenantId,
    CancellationToken cancellationToken = default);

  /// <summary>
  /// Builds a server-scoped descriptor. It carries no tenant, which is what grants cross-tenant reach.
  /// </summary>
  ResourceDescriptor CreateServer();

  /// <summary>
  /// Builds a tenant-scoped descriptor for the given tenant.
  /// </summary>
  ResourceDescriptor CreateTenant(Guid tenantId);
}

/// <inheritdoc cref="IResourceDescriptorFactory"/>
public sealed class ResourceDescriptorFactory(
  IDbContextFactory<AppDb> dbContextFactory) : IResourceDescriptorFactory
{
  private readonly IDbContextFactory<AppDb> _dbContextFactory = dbContextFactory;

  /// <inheritdoc/>
  public async Task<ResourceDescriptor> CreateDevice(
    Device device,
    CancellationToken cancellationToken = default)
  {
    IReadOnlyCollection<Guid> deviceGroupIds;
    if (device.DeviceGroupMembers is not null)
    {
      deviceGroupIds = [.. device.DeviceGroupMembers.Select(member => member.DeviceGroupId)];
    }
    else
    {
      await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
      deviceGroupIds = await db.DeviceGroupMembers
        .IgnoreQueryFilters()
        .AsNoTracking()
        .Where(member => member.DeviceId == device.Id)
        .Select(member => member.DeviceGroupId)
        .ToListAsync(cancellationToken);
    }

    return new ResourceDescriptor(
      PermissionScopeKind.Device,
      device.Id,
      device.TenantId,
      device.CustomerId,
      deviceGroupIds);
  }

  /// <inheritdoc/>
  public async Task<ResourceDescriptor?> CreateScope(
    PermissionScopeKind scopeKind,
    Guid? scopeId,
    Guid tenantId,
    CancellationToken cancellationToken = default)
  {
    if (scopeKind == PermissionScopeKind.Server)
    {
      return CreateServer();
    }

    if (scopeKind == PermissionScopeKind.Tenant)
    {
      return scopeId is null || scopeId == tenantId ? CreateTenant(tenantId) : null;
    }

    if (!scopeId.HasValue)
    {
      return null;
    }

    await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
    switch (scopeKind)
    {
      case PermissionScopeKind.DeviceGroup:
        return await db.DeviceGroups
          .IgnoreQueryFilters()
          .AsNoTracking()
          .AnyAsync(group => group.Id == scopeId.Value && group.TenantId == tenantId, cancellationToken)
            ? new ResourceDescriptor(scopeKind, scopeId, tenantId)
            : null;

      case PermissionScopeKind.CustomerTenant:
        return await db.Customers
          .IgnoreQueryFilters()
          .AsNoTracking()
          .AnyAsync(customer => customer.Id == scopeId.Value && customer.TenantId == tenantId, cancellationToken)
            ? new ResourceDescriptor(scopeKind, scopeId, tenantId)
            : null;

      case PermissionScopeKind.UserGroup:
        return await db.UserGroups
          .IgnoreQueryFilters()
          .AsNoTracking()
          .AnyAsync(group => group.Id == scopeId.Value && group.TenantId == tenantId, cancellationToken)
            ? new ResourceDescriptor(scopeKind, scopeId, tenantId)
            : null;

      case PermissionScopeKind.Device:
        var device = await db.Devices
          .IgnoreQueryFilters()
          .AsNoTracking()
          .FirstOrDefaultAsync(
            candidate => candidate.Id == scopeId.Value && candidate.TenantId == tenantId,
            cancellationToken);
        return device is null ? null : await CreateDevice(device, cancellationToken);

      default:
        return null;
    }
  }

  /// <inheritdoc/>
  public ResourceDescriptor CreateServer() => new(PermissionScopeKind.Server);

  /// <inheritdoc/>
  public ResourceDescriptor CreateTenant(Guid tenantId) =>
    new(PermissionScopeKind.Tenant, tenantId, tenantId);
}
