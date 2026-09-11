using ControlR.Libraries.Hosting;
using ControlR.Web.Server.Services.Authorization;
using Microsoft.Extensions.Caching.Memory;

namespace ControlR.Web.Server.Services.ServiceAccounts;

/// <summary>
/// Periodically deletes service account credentials that were revoked or expired longer ago
/// than <see cref="AppOptions.ServiceAccountCredentialCleanupAfterDays"/>. Applies to both
/// server-scoped and tenant-scoped credentials. Each deletion is recorded in the
/// <see cref="AuthorizationChangeLog"/> with a system actor. Rows are removed in batches,
/// each with a fresh <see cref="AppDb"/> so the change tracker stays bounded regardless of
/// backlog size.
/// </summary>
public class ServiceAccountCredentialCleanupBackgroundService(
  IDbContextFactory<AppDb> dbContextFactory,
  IAuthorizationChangeLogFactory changeLogFactory,
  IMemoryCache memoryCache,
  IOptions<AppOptions> appOptions,
  TimeProvider timeProvider,
  ILogger<PeriodicBackgroundService> logger)
  : PeriodicBackgroundService(TimeSpan.FromHours(12), true, timeProvider, logger)
{
  private const int CleanupBatchSize = 200;

  private readonly IOptions<AppOptions> _appOptions = appOptions;
  private readonly IAuthorizationChangeLogFactory _changeLogFactory = changeLogFactory;
  private readonly IDbContextFactory<AppDb> _dbContextFactory = dbContextFactory;
  private readonly ILogger _logger = logger;
  private readonly IMemoryCache _memoryCache = memoryCache;
  private readonly TimeProvider _timeProvider = timeProvider;

  public async Task<int> CleanDeadCredentials(CancellationToken cancellationToken = default)
  {
    var cutoff = GetCleanupCutoff();
    if (!cutoff.HasValue)
    {
      return 0;
    }

    var totalRemoved = 0;
    while (true)
    {
      cancellationToken.ThrowIfCancellationRequested();

      var removedCount = await RemoveBatch(cutoff.Value, cancellationToken);
      if (removedCount == 0)
      {
        break;
      }

      totalRemoved += removedCount;
    }

    if (totalRemoved > 0)
    {
      _logger.LogInformation(
        "Removed {RemovedCount} revoked or expired service account credential(s) older than {Cutoff}.",
        totalRemoved,
        cutoff.Value);
    }

    return totalRemoved;
  }

  protected override async Task HandleElapsed(CancellationToken stoppingToken)
  {
    await CleanDeadCredentials(stoppingToken);
  }

  protected override async Task OnStartingAsync(CancellationToken stoppingToken)
  {
    await CleanDeadCredentials(stoppingToken);
  }

  private DateTimeOffset? GetCleanupCutoff()
  {
    var cleanupDays = _appOptions.Value.ServiceAccountCredentialCleanupAfterDays;
    if (cleanupDays <= 0)
    {
      return null;
    }

    return _timeProvider.GetUtcNow() - TimeSpan.FromDays(cleanupDays);
  }

  private async Task<int> RemoveBatch(DateTimeOffset cutoff, CancellationToken cancellationToken)
  {
    await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

    var batch = await db.ServiceAccountCredentials
      .IgnoreQueryFilters()
      .Include(x => x.ServiceAccount)
      .Where(x => (x.RevokedAt < cutoff) || (x.ExpiresAt < cutoff))
      .OrderBy(x => x.Id)
      .Take(CleanupBatchSize)
      .ToListAsync(cancellationToken);

    if (batch.Count == 0)
    {
      return 0;
    }

    foreach (var credential in batch)
    {
      db.AuthorizationChangeLogs.Add(_changeLogFactory.Create(
        AuthorizationChangeLogActions.ServiceAccountCredentialDeleted,
        actor: null,
        AuthorizationChangeLogTargetTypes.ServiceAccountCredential,
        credential.Id,
        credential.ServiceAccount?.TenantId,
        before: new ServiceAccountCredentialSnapshot(credential.Name, credential.ServiceAccountId)));
    }

    db.ServiceAccountCredentials.RemoveRange(batch);
    await db.SaveChangesAsync(cancellationToken);

    // Mirror ServiceAccountManager.PurgeCredentialAsync: drop any cached validation
    // result so a deleted credential's API key fails immediately instead of waiting
    // out the cache TTL.
    foreach (var credential in batch)
    {
      _memoryCache.Remove(credential.Id);
    }

    return batch.Count;
  }
}
