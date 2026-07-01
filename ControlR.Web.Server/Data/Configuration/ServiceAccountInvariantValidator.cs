using Microsoft.EntityFrameworkCore.Diagnostics;
using ControlR.Web.Server.Data.Enums;

namespace ControlR.Web.Server.Data.Configuration;

public sealed class ServiceAccountInvariantInterceptor : SaveChangesInterceptor
{
  private const string ServerTenantMismatch = "Server-scoped service accounts must have a null TenantId.";
  private const string TenantMissingTenantId = "Tenant-scoped service accounts must have a non-null TenantId.";

  public override InterceptionResult<int> SavingChanges(
    DbContextEventData eventData,
    InterceptionResult<int> result)
  {
    ValidateKindTenantInvariant(eventData.Context);
    return base.SavingChanges(eventData, result);
  }

  public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
    DbContextEventData eventData,
    InterceptionResult<int> result,
    CancellationToken cancellationToken = default)
  {
    if (eventData.Context is not null)
    {
      ValidateKindTenantInvariant(eventData.Context);
      await ValidateNameUniqueness(eventData.Context, cancellationToken);
    }
    return await base.SavingChangesAsync(eventData, result, cancellationToken);
  }

  private static bool Conflict(ServiceAccount a, ServiceAccount b)
  {
    return a.Kind == b.Kind &&
           a.Name == b.Name &&
           (a.TenantId == b.TenantId || (a.TenantId is null && b.TenantId is null));
  }

  private static void ValidateKindTenantInvariant(DbContext? context)
  {
    if (context is null) return;

    foreach (var entry in context.ChangeTracker.Entries<ServiceAccount>())
    {
      if (entry.State is not (EntityState.Added or EntityState.Modified))
        continue;

      var account = entry.Entity;
      if (account.Kind == ServiceAccountKind.Server && account.TenantId.HasValue)
        throw new InvalidOperationException(ServerTenantMismatch);
      if (account.Kind == ServiceAccountKind.Tenant && !account.TenantId.HasValue)
        throw new InvalidOperationException(TenantMissingTenantId);
    }
  }

  private static async Task ValidateNameUniqueness(DbContext context, CancellationToken cancellationToken)
  {
    var candidates = context.ChangeTracker.Entries<ServiceAccount>()
      .Where(e => e.State is EntityState.Added or EntityState.Modified)
      .Select(e => e.Entity)
      .ToList();

    if (candidates.Count == 0) return;

    // Check for duplicate (Kind, TenantId, Name) within the same batch.
    for (var i = 0; i < candidates.Count; i++)
    {
      for (var j = i + 1; j < candidates.Count; j++)
      {
        if (Conflict(candidates[i], candidates[j]))
        {
          throw new InvalidOperationException(
            $"Duplicate service account name '{candidates[i].Name}' within the same batch.");
        }
      }
    }

    // Check for conflicts with existing rows in the database.
    var candidateNames = candidates.Select(a => a.Name).ToHashSet();

    var conflicts = await context.Set<ServiceAccount>()
      .AsNoTracking()
      .Where(x => candidateNames.Contains(x.Name) && !candidates.Select(c => c.Id).Contains(x.Id))
      .Select(x => new { x.Kind, x.TenantId, x.Name })
      .ToListAsync(cancellationToken);

    foreach (var candidate in candidates)
    {
      // using a local variable to avoid EF Core translating candidate.TenantId == null to client eval
      var candidateTenantId = candidate.TenantId;

      var found = conflicts.Any(c =>
        c.Kind == candidate.Kind &&
        (c.TenantId == null ? candidateTenantId == null : c.TenantId == candidateTenantId) &&
        c.Name == candidate.Name);

      if (found)
      {
        throw new InvalidOperationException(
          $"A service account named '{candidate.Name}' already exists for this kind and tenant.");
      }
    }
  }
}
