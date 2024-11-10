using System.Security.Claims;
using ControlR.Web.Client.Extensions;
using ControlR.Web.Server.Data.Entities.Bases;

namespace ControlR.Web.Server.Extensions;

public static class EntityFilterExtensions
{
  public static IQueryable<T> FilterByTenantId<T>(
    this IQueryable<T> query,
    ClaimsPrincipal principal) where T : ITenantEntityBase
  {
    if (!principal.TryGetTenantId(out var tenantId))
    {
      throw new InvalidOperationException(
        $"{nameof(FilterByTenantId)} should only be called within the scope of an authenticated request.");
    }

    return query.Where(x => x.Id == tenantId);
  }
}