using ControlR.Web.Server.Services.DeviceManagement;

namespace ControlR.Web.Server.Extensions;

public static class DeviceAccessQueryExtensions
{
  public static IQueryable<Device> ApplyAccessScope(
    this IQueryable<Device> query,
    Guid tenantId,
    DeviceAccessScope accessScope)
  {
    // Establish a deterministic default ordering at the start of the query so that
    // any downstream operators (Take/Skip, paging) always operate on an ordered
    // query. Callers may still override the ordering via ApplySorting; explicit
    // sorts issued by the caller replace this default.
    query = query.Where(x => x.TenantId == tenantId).OrderBy(x => x.CreatedAt);

    return accessScope.Kind switch
    {
      DeviceAccessScopeKind.TenantWide => query,
      DeviceAccessScopeKind.SingleDevice => query.Where(x => x.Id == accessScope.DeviceId),
      DeviceAccessScopeKind.TaggedDevices => query.Where(x => x.Tags!.Any(tag => accessScope.TagIds.Contains(tag.Id))),
      _ => query.Where(_ => false)
    };
  }
}