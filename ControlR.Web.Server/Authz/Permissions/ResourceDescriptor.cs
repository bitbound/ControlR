namespace ControlR.Web.Server.Authz.Permissions;

/// <summary>
/// The resource a permission decision is evaluated against. A stored grant reaches this
/// resource when its scope matches one of the values on this descriptor. See
/// <c>PermissionScopeMatcher.Matches</c> for the match rules.
/// </summary>
/// <param name="Kind">The scope kind of the resource.</param>
/// <param name="Id">The id of the resource row. A grant at the resource's own kind matches on this value.</param>
/// <param name="TenantId">The tenant that owns the resource. A Tenant-scoped grant matches on this value. Null for the server descriptor.</param>
/// <param name="CustomerId">The customer the resource belongs to. Set on device descriptors so a CustomerTenant grant can reach a device. Null otherwise.</param>
/// <param name="DeviceGroupIds">The device groups the resource belongs to. Set on device descriptors so a DeviceGroup grant can reach a device. Null otherwise.</param>
public sealed record ResourceDescriptor(
  PermissionScopeKind Kind,
  Guid? Id = null,
  Guid? TenantId = null,
  Guid? CustomerId = null,
  IReadOnlyCollection<Guid>? DeviceGroupIds = null);
