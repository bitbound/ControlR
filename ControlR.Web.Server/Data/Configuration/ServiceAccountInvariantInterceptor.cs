using Microsoft.EntityFrameworkCore.Diagnostics;
using ControlR.Web.Server.Data.Enums;

namespace ControlR.Web.Server.Data.Configuration;

/// <summary>
/// In-memory enforcement of the Kind/TenantId invariant for tests using the EF in-memory
/// provider. PostgreSQL enforces this via the CK_ServiceAccounts_Kind_TenantId check
/// constraint and the filtered unique indexes in production. Name uniqueness is enforced
/// exclusively by the database (the filtered indexes) and is not checked here.
/// </summary>
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
    }

    return base.SavingChanges(eventData, result);
  }

  public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
    DbContextEventData eventData,
    InterceptionResult<int> result,
    CancellationToken cancellationToken = default)
  {
    if (eventData.Context is not null)
    {
      ValidateKindTenantInvariant(eventData.Context);
    }

    return base.SavingChangesAsync(eventData, result, cancellationToken);
  }

  private static void ValidateKindTenantInvariant(DbContext context)
  {
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
}
