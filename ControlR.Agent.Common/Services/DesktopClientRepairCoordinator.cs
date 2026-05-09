using Microsoft.Extensions.Hosting;

namespace ControlR.Agent.Common.Services;

internal interface IDesktopClientRepairCoordinator
{
  void ReportFailure(string key, string reason, bool immediate = false);
  void ReportHealthy(string key);
}

internal sealed class DesktopClientRepairCoordinator(
  TimeProvider timeProvider,
  IAgentMaintenanceService agentUpdater,
  IHostApplicationLifetime appLifetime,
  ILogger<DesktopClientRepairCoordinator> logger) : IDesktopClientRepairCoordinator
{
  private const int FailureThreshold = 3;

  private static readonly TimeSpan _failureWindow = TimeSpan.FromSeconds(30);
  private static readonly TimeSpan _repairCooldown = TimeSpan.FromMinutes(10);

  private readonly IAgentMaintenanceService _agentUpdater = agentUpdater;
  private readonly IHostApplicationLifetime _appLifetime = appLifetime;
  private readonly Dictionary<string, RepairFailureState> _failureStates = [];
  private readonly ILogger<DesktopClientRepairCoordinator> _logger = logger;
  private readonly Lock _syncRoot = new();
private readonly TimeProvider _timeProvider = timeProvider;
  private DateTimeOffset _cooldownUntil = DateTimeOffset.MinValue;
  private bool _repairQueuedOrRunning;

  public void ReportFailure(string key, string reason, bool immediate = false)
  {
    if (string.IsNullOrWhiteSpace(key))
    {
      throw new ArgumentException("Failure key must be provided.", nameof(key));
    }

    if (string.IsNullOrWhiteSpace(reason))
    {
      reason = "Desktop client health check failed.";
    }

    var now = _timeProvider.GetUtcNow();
    var shouldQueueRepair = false;

    lock (_syncRoot)
    {
      _failureStates.TryGetValue(key, out var state);
      state = state?.Record(now, reason) ?? new RepairFailureState(1, now, reason);
      _failureStates[key] = state;

      if (!immediate &&
          state.Count < FailureThreshold &&
          now - state.FirstFailureAt < _failureWindow)
      {
        return;
      }

      if (_repairQueuedOrRunning)
      {
        _logger.LogInformation(
          "Desktop repair already in progress. Suppressing duplicate request for {Key}. Reason: {Reason}",
          key,
          reason);
        return;
      }

      if (now < _cooldownUntil)
      {
        _logger.LogInformation(
          "Desktop repair request for {Key} is in cooldown until {CooldownUntil}. Reason: {Reason}",
          key,
          _cooldownUntil,
          reason);
        return;
      }

      _cooldownUntil = now + _repairCooldown;
      _repairQueuedOrRunning = true;
      shouldQueueRepair = true;
    }

    if (!shouldQueueRepair)
    {
      return;
    }

    Task.Run(async () =>
    {
      try
      {
        _logger.LogWarning("Queueing desktop repair. Reason: {Reason}", reason);
        await _agentUpdater.RepairDesktopClient(reason, _appLifetime.ApplicationStopping);
      }
      catch (OperationCanceledException)
      {
        _logger.LogInformation("Desktop repair canceled.");
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Desktop repair failed.");
      }
      finally
      {
        lock (_syncRoot)
        {
          _repairQueuedOrRunning = false;
        }
      }
    }).Forget();
  }

  public void ReportHealthy(string key)
  {
    if (string.IsNullOrWhiteSpace(key))
    {
      return;
    }

    lock (_syncRoot)
    {
      _failureStates.Remove(key);
    }
  }

  private sealed record RepairFailureState(int Count, DateTimeOffset FirstFailureAt, string LastReason)
  {
    public RepairFailureState Record(DateTimeOffset timestamp, string reason)
    {
      if (Count == 0)
      {
        return new RepairFailureState(1, timestamp, reason);
      }

      if (timestamp - FirstFailureAt > _failureWindow)
      {
        return new RepairFailureState(1, timestamp, reason);
      }

      return new RepairFailureState(Count + 1, FirstFailureAt, reason);
    }
  }
}