using ControlR.Libraries.Hosting;

namespace ControlR.Web.Server.Services.AgentInstaller;

public class AgentInstallerKeyUsageCleanupBackgroundService(
  AgentInstallerKeyUsageCleaner cleaner,
  TimeProvider timeProvider,
  ILogger<PeriodicBackgroundService> logger)
  : PeriodicBackgroundService(TimeSpan.FromHours(8), true, timeProvider, logger)
{
  private readonly AgentInstallerKeyUsageCleaner _cleaner = cleaner;

  protected override async Task HandleElapsed()
  {
    await _cleaner.CleanExpiredUsages();
  }

  protected override async Task OnStartingAsync(CancellationToken stoppingToken)
  {
    await _cleaner.CleanExpiredUsages(stoppingToken);
  }
}