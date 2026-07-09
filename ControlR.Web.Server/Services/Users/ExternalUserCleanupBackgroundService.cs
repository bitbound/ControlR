using ControlR.Libraries.Hosting;
using ControlR.Web.Server.Data.Enums;

namespace ControlR.Web.Server.Services.Users;

public class ExternalUserCleanupBackgroundService(
  IDbContextFactory<AppDb> dbContextFactory,
  IOptions<AppOptions> appOptions,
  TimeProvider timeProvider,
  ILogger<PeriodicBackgroundService> logger)
  : PeriodicBackgroundService(TimeSpan.FromDays(1), true, timeProvider, logger)
{
  private readonly IOptions<AppOptions> _appOptions = appOptions;
  private readonly IDbContextFactory<AppDb> _dbContextFactory = dbContextFactory;
  private readonly ILogger _logger = logger;
  private readonly TimeProvider _timeProvider = timeProvider;

  public async Task<int> CleanExpiredExternalUsers(CancellationToken cancellationToken = default)
  {
    var cutoff = GetExternalUserCleanupCutoff();
    if (!cutoff.HasValue)
    {
      return 0;
    }

    await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
    var query = db.Users
      .Where(x => x.AccountType == AccountType.ExternalUser)
      .Where(x => x.LastLogin != null && x.LastLogin < cutoff.Value);

    int removedCount;

    if (db.Database.IsRelational())
    {
      removedCount = await query.ExecuteDeleteAsync(cancellationToken);
    }
    else
    {
      var expiredUsers = await query.ToListAsync(cancellationToken);
      removedCount = expiredUsers.Count;

      if (removedCount > 0)
      {
        db.Users.RemoveRange(expiredUsers);
        await db.SaveChangesAsync(cancellationToken);
      }
    }

    if (removedCount > 0)
    {
      _logger.LogInformation(
        "Removed {RemovedCount} expired external user accounts with last login before {Cutoff}.",
        removedCount,
        cutoff.Value);
    }

    return removedCount;
  }

  protected override async Task HandleElapsed()
  {
    await CleanExpiredExternalUsers();
  }

  protected override async Task OnStartingAsync(CancellationToken stoppingToken)
  {
    await CleanExpiredExternalUsers(stoppingToken);
  }

  private DateTimeOffset? GetExternalUserCleanupCutoff()
  {
    var cleanupDays = _appOptions.Value.ExternalUserCleanupAfterDays;
    if (cleanupDays <= 0)
    {
      return null;
    }

    return _timeProvider.GetUtcNow() - TimeSpan.FromDays(cleanupDays);
  }
}
