
using System.Collections.Concurrent;
using ControlR.Agent.Common.Services.Windows.Internals;
using ControlR.Libraries.Api.Contracts.Dtos.Devices;
using ControlR.Libraries.Shared.Services.Processes;

namespace ControlR.Agent.Common.Services.Windows;

internal interface IDesktopClientLaunchTracker
{
  void Clear();
  bool IsSessionCovered(int sessionId, IReadOnlyCollection<DesktopSession> desktopClients);
  void Reconcile(IReadOnlySet<int> activeSessionIds, IReadOnlyCollection<DesktopSession> desktopClients);
  void TrackLaunch(int sessionId, IProcess process);
  bool TryRemove(int sessionId, int processId, out DesktopClientLaunchState? removedState);
}

internal sealed class DesktopClientLaunchTracker(
  TimeProvider timeProvider, 
  ILogger<DesktopClientLaunchTracker> logger) : IDesktopClientLaunchTracker
{
  internal static readonly TimeSpan StartupGracePeriod = TimeSpan.FromSeconds(45);

  private readonly ILogger<DesktopClientLaunchTracker> _logger = logger;
  private readonly TimeProvider _timeProvider = timeProvider;
  private readonly ConcurrentDictionary<int, DesktopClientLaunchState> _trackedLaunches = [];

  internal int Count => _trackedLaunches.Count;

  public void Clear()
  {
    foreach (var kvp in _trackedLaunches)
    {
      if (_trackedLaunches.TryRemove(kvp.Key, out var removedState))
      {
        removedState.Dispose();
      }
    }
  }

  public bool IsSessionCovered(int sessionId, IReadOnlyCollection<DesktopSession> desktopClients)
  {
    if (desktopClients.Any(x => x.SystemSessionId == sessionId))
    {
      return true;
    }

    if (!_trackedLaunches.TryGetValue(sessionId, out var launchState))
    {
      return false;
    }

    if (!IsProcessAlive(launchState.Process))
    {
      return false;
    }

    var elapsed = _timeProvider.GetUtcNow() - launchState.StartedAt;
    if (elapsed > StartupGracePeriod)
    {
      return false;
    }

    _logger.LogDebug(
      "Treating session {SessionId} as covered by pending desktop client launch. PID: {ProcessId}, ElapsedMs: {ElapsedMs}",
      sessionId,
      launchState.ProcessId,
      elapsed.TotalMilliseconds);

    return true;
  }

  public void Reconcile(IReadOnlySet<int> activeSessionIds, IReadOnlyCollection<DesktopSession> desktopClients)
  {
    var registeredBySession = desktopClients
      .GroupBy(x => x.SystemSessionId)
      .ToDictionary(x => x.Key, x => x.First());

    foreach (var kvp in _trackedLaunches.ToArray())
    {
      var launchState = kvp.Value;

      if (!activeSessionIds.Contains(launchState.SessionId))
      {
        Remove(
          launchState.SessionId,
          $"Tracked launch is no longer needed because session {launchState.SessionId} is no longer active.");
        continue;
      }

      if (registeredBySession.TryGetValue(launchState.SessionId, out var registeredSession))
      {
        Remove(
          launchState.SessionId,
          $"IPC registration observed for tracked desktop client launch. Session: {launchState.SessionId}, " +
          $"Tracked PID: {launchState.ProcessId}, Registered PID: {registeredSession.ProcessId}");
        continue;
      }

      if (launchState.Process.SessionId != launchState.SessionId)
      {
        Remove(
          launchState.SessionId,
          $"Removing stale tracked launch for session {launchState.SessionId}. " +
          $"Tracked PID: {launchState.ProcessId}, Process session: {launchState.Process.SessionId}");
        continue;
      }

      if (!IsProcessAlive(launchState.Process))
      {
        Remove(
          launchState.SessionId,
          $"Tracked desktop client exited before IPC registration completed. " +
          $"Session: {launchState.SessionId}, PID: {launchState.ProcessId}");
        continue;
      }

      var elapsed = _timeProvider.GetUtcNow() - launchState.StartedAt;
      if (elapsed <= StartupGracePeriod)
      {
        continue;
      }

      Remove(
        launchState.SessionId,
        $"Tracked desktop client launch aged out without IPC registration. " +
        $"Session: {launchState.SessionId}, PID: {launchState.ProcessId}, ElapsedMs: {elapsed.TotalMilliseconds}");
    }
  }

  public void TrackLaunch(int sessionId, IProcess process)
  {
    var launchState = new DesktopClientLaunchState(
      sessionId,
      process.Id,
      process,
      _timeProvider.GetUtcNow());

    _trackedLaunches.AddOrUpdate(
      sessionId,
      launchState,
      (_, existing) =>
      {
        _logger.LogWarning(
          "Replacing existing tracked desktop client launch. Session: {SessionId}, Old PID: {OldProcessId}, New PID: {NewProcessId}",
          sessionId,
          existing.ProcessId,
          process.Id);

        existing.Dispose();
        return launchState;
      });
  }

  public bool TryRemove(int sessionId, int processId, out DesktopClientLaunchState? removedState)
  {
    removedState = null;

    if (!_trackedLaunches.TryGetValue(sessionId, out var launchState) || launchState.ProcessId != processId)
    {
      return false;
    }

    return _trackedLaunches.TryRemove(sessionId, out removedState);
  }

  private static bool IsProcessAlive(IProcess process)
  {
    try
    {
      return !process.HasExited;
    }
    catch
    {
      return false;
    }
  }

  private void Remove(int sessionId, string message)
  {
    if (_trackedLaunches.TryRemove(sessionId, out var removedState))
    {
      _logger.LogInformation(message);
      removedState.Dispose();
    }
  }
}
