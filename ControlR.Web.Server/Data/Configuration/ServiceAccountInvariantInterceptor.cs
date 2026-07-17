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
    if (eventData.Context is not null)
    {
      ValidateKindTenantInvariant(eventData.Context);
      ValidateNameUniqueness(eventData.Context);
    }

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
      await ValidateNameUniquenessAsync(eventData.Context, cancellationToken);
    }
    return await base.SavingChangesAsync(eventData, result, cancellationToken);
  }

  private static bool Conflict(ServiceAccount a, ServiceAccount b)
  {
    return a.Kind == b.Kind &&
           a.Name == b.Name &&
           a.TenantId == b.TenantId;
  }

  private static List<ServiceAccount> GetCandidateAccounts(DbContext context)
  {
    return context.ChangeTracker.Entries<ServiceAccount>()
      .Where(e => e.State is EntityState.Added or EntityState.Modified)
      .Select(e => e.Entity)
      .ToList();
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

  private static void ValidateNameUniqueness(DbContext context)
  {
    var candidates = GetCandidateAccounts(context);

    if (candidates.Count == 0) return;

    ValidateNameUniqueness(candidates, context.Set<ServiceAccount>()
      .AsNoTracking()
      .Where(x => candidates.Select(c => c.Name).Contains(x.Name) && !candidates.Select(c => c.Id).Contains(x.Id))
      .Select(x => new ServiceAccount
      {
        Id = x.Id,
        Kind = x.Kind,
        TenantId = x.TenantId,
        Name = x.Name,
      })
      .ToList());
  }

  private static void ValidateNameUniqueness(
    IReadOnlyList<ServiceAccount> candidates,
    IReadOnlyList<ServiceAccount> existingAccounts)
  {
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

    foreach (var candidate in candidates)
    {
      var found = existingAccounts.Any(existing => Conflict(existing, candidate));

      if (found)
      {
        throw new InvalidOperationException(
          $"A service account named '{candidate.Name}' already exists for this kind and tenant.");
      }
    }
  }

  private static async Task ValidateNameUniquenessAsync(DbContext context, CancellationToken cancellationToken)
  {
    var candidates = GetCandidateAccounts(context);

    if (candidates.Count == 0) return;

    var candidateNames = candidates.Select(a => a.Name).ToHashSet();

    ValidateNameUniqueness(candidates, await context.Set<ServiceAccount>()
      .AsNoTracking()
      .Where(x => candidateNames.Contains(x.Name) && !candidates.Select(c => c.Id).Contains(x.Id))
      .Select(x => new ServiceAccount
      {
        Id = x.Id,
        Kind = x.Kind,
        TenantId = x.TenantId,
        Name = x.Name,
      })
      .ToListAsync(cancellationToken));
  }
}
