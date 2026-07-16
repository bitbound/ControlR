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
    var expiredUserIds = await db.Users
      .Where(x => x.AccountType == AccountType.ExternalUser)
      .Where(x => x.LastLogin != null && x.LastLogin < cutoff.Value)
      .Select(x => x.Id)
      .ToListAsync(cancellationToken);

    if (expiredUserIds.Count == 0)
    {
      return 0;
    }

    if (db.Database.IsRelational())
    {
      await db.Users
        .Where(x => expiredUserIds.Contains(x.Id))
        .ExecuteDeleteAsync(cancellationToken);
    }
    else
    {
      var expiredUsers = await db.Users
        .Where(x => expiredUserIds.Contains(x.Id))
        .ToListAsync(cancellationToken);
      db.Users.RemoveRange(expiredUsers);
      await db.SaveChangesAsync(cancellationToken);
    }

    _logger.LogWarning(
      "Removed {RemovedCount} expired external user accounts with last login before {Cutoff}. User IDs: {UserIds}",
      expiredUserIds.Count,
      cutoff.Value,
      expiredUserIds);

    return expiredUserIds.Count;
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

    // 0 or negative disables cleanup by config intent. Sub-day values are
    // treated the same way because a half-day cutoff would mass-delete most
    // external users on each run — almost certainly a misconfiguration.
    if (cleanupDays < 1)
    {
      if (cleanupDays > 0)
      {
        _logger.LogError(
          "External user cleanup is disabled: ExternalUserCleanupAfterDays is {CleanupDays}, which is less than 1 day. " +
          "Sub-day values are likely a misconfiguration.",
          cleanupDays);
      }
      return null;
    }

    return _timeProvider.GetUtcNow() - TimeSpan.FromDays(cleanupDays);
  }
}
