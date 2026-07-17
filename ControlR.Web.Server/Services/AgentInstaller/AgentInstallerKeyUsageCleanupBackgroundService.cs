using ControlR.Libraries.Hosting;

namespace ControlR.Web.Server.Services.AgentInstaller;

public class AgentInstallerKeyUsageCleanupBackgroundService(
  IDbContextFactory<AppDb> dbContextFactory,
  IOptions<AppOptions> appOptions,
  TimeProvider timeProvider,
  ILogger<PeriodicBackgroundService> logger)
  : PeriodicBackgroundService(TimeSpan.FromHours(8), true, timeProvider, logger)
{
  private readonly IOptions<AppOptions> _appOptions = appOptions;
  private readonly IDbContextFactory<AppDb> _dbContextFactory = dbContextFactory;
  private readonly ILogger _logger = logger;
  private readonly TimeProvider _timeProvider = timeProvider;

  public async Task<int> CleanExpiredUsages(CancellationToken cancellationToken = default)
  {
    var cutoff = GetUsageHistoryCutoff();
    if (!cutoff.HasValue)
    {
      return 0;
    }

    await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
    var query = db.AgentInstallerKeyUsages
      .Where(x => x.CreatedAt < cutoff.Value);

    int removedCount;

    if (db.Database.IsRelational())
    {
      removedCount = await query.ExecuteDeleteAsync(cancellationToken);
    }
    else
    {
      var expiredUsages = await query.ToListAsync(cancellationToken);
      removedCount = expiredUsages.Count;

      if (removedCount > 0)
      {
        db.AgentInstallerKeyUsages.RemoveRange(expiredUsages);
        await db.SaveChangesAsync(cancellationToken);
      }
    }

    if (removedCount > 0)
    {
      _logger.LogInformation(
        "Removed {RemovedCount} expired installer key usage records older than {Cutoff}.",
        removedCount,
        cutoff.Value);
    }

    return removedCount;
  }

  protected override async Task HandleElapsed()
  {
    await CleanExpiredUsages();
  }

  protected override async Task OnStartingAsync(CancellationToken stoppingToken)
  {
    await CleanExpiredUsages(stoppingToken);
  }

  private DateTimeOffset? GetUsageHistoryCutoff()
  {
    var historyDays = _appOptions.Value.AgentInstallerKeyHistoryDays;
    if (historyDays <= 0)
    {
      return null;
    }

    return _timeProvider.GetUtcNow() - TimeSpan.FromDays(historyDays);
  }
}
