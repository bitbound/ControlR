using ControlR.Libraries.Hosting;

namespace ControlR.Web.Server.Services.ExternalUsers;

public class ExternalUserCleanupBackgroundService(
  ExternalUserCleanupService cleanupService,
  TimeProvider timeProvider,
  ILogger<PeriodicBackgroundService> logger)
  : PeriodicBackgroundService(
      period: TimeSpan.FromDays(1),
      catchExceptions: true,
      timeProvider,
      logger)
{
  private readonly ExternalUserCleanupService _cleanupService = cleanupService;

  protected override async Task HandleElapsed()
  {
    await _cleanupService.CleanExpiredExternalUsers();
  }

  protected override async Task OnStartingAsync(CancellationToken stoppingToken)
  {
    await _cleanupService.CleanExpiredExternalUsers(stoppingToken);
  }
}
