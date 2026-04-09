using ControlR.Libraries.Shared.Extensions;
using ControlR.Libraries.Shared.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ControlR.Libraries.Hosting;

public abstract class PeriodicBackgroundService(
  TimeSpan period,
  TimeProvider timeProvider, 
  ILogger<PeriodicBackgroundService> logger) : BackgroundService
{
  protected readonly ILogger<PeriodicBackgroundService> Logger = logger;

  private LogDeduplicationContext<PeriodicBackgroundService>? _dedupeLogger;

  protected LogDeduplicationContext<PeriodicBackgroundService> DedupeLogger =>
    _dedupeLogger ?? throw new InvalidOperationException("Deduplication context is not active.");

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    using var dedupeScope = Logger.EnterDedupeScope();
    _dedupeLogger = dedupeScope;
    using var timer = new PeriodicTimer(period, timeProvider);
    while (await timer.WaitForNextTick(throwOnCancellation: false, stoppingToken))
    {
      try
      {
        await HandleElapsed();
      }
      catch (OperationCanceledException)
      {
        Logger.LogInformation("Background service is stopping due to cancellation.");
        break;
      }
      catch (Exception ex)
      {
        Logger.LogError(ex, "Error in periodic background service.");
      }
    }

    Logger.LogInformation("Stopping background service. Application is stopping.");
    _dedupeLogger = null;
  }

  protected abstract Task HandleElapsed();
}
