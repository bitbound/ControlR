using ControlR.Libraries.Api.Contracts.Hubs.Clients;
using ControlR.Libraries.Hosting;
using Microsoft.AspNetCore.SignalR;

namespace ControlR.Web.Server.Services.Assist;

/// <summary>
/// Ages out assist sessions that stalled: a consent prompt nobody answered, a code nobody used,
/// or a session that outlived any sane support call. Normal ends are disconnect-driven; this is
/// the safety net.
/// </summary>
public class AssistSessionCleanupBackgroundService(
  IAssistSessionCache sessionCache,
  IHubContext<AssistHub, IAssistHubClient> assistHub,
  TimeProvider timeProvider,
  ILogger<PeriodicBackgroundService> logger)
  : PeriodicBackgroundService(
    period: TimeSpan.FromSeconds(30),
    catchExceptions: true,
    timeProvider: timeProvider,
    logger: logger)
{
  private static readonly TimeSpan _activeExpiry = TimeSpan.FromHours(12);
  private static readonly TimeSpan _consentExpiry = TimeSpan.FromMinutes(1);
  private static readonly TimeSpan _waitingExpiry = TimeSpan.FromMinutes(30);

  private readonly IHubContext<AssistHub, IAssistHubClient> _assistHub = assistHub;
  private readonly IAssistSessionCache _sessionCache = sessionCache;
  private readonly TimeProvider _timeProvider = timeProvider;

  protected override async Task HandleElapsed(CancellationToken stoppingToken)
  {
    var now = _timeProvider.GetUtcNow();

    foreach (var session in _sessionCache.Snapshot())
    {
      stoppingToken.ThrowIfCancellationRequested();
      var age = now - session.StatusChangedAt;

      switch (session.Status)
      {
        case AssistSessionStatus.PendingConsent when age > _consentExpiry:
          {
            if (_sessionCache.TryReturnToWaiting(session.SessionId, out _) &&
                session.ViewerConnectionId is { Length: > 0 } viewerConnectionId)
            {
              await _assistHub.Clients
                .Client(viewerConnectionId)
                .AssistConsentResponded(new(session.SessionId, false, null));
            }
            break;
          }
        case AssistSessionStatus.WaitingForViewer when age > _waitingExpiry:
          {
            _ = _sessionCache.Remove(session.SessionId);
            break;
          }
        case AssistSessionStatus.Active when age > _activeExpiry:
          {
            _ = _sessionCache.Remove(session.SessionId);
            await NotifyEnded(session.SessionId, "The session expired.", session);
            break;
          }
      }
    }
  }

  private async Task NotifyEnded(Guid sessionId, string reason, AssistSession session)
  {
    if (session.ViewerConnectionId is { Length: > 0 } viewerConnectionId)
    {
      await _assistHub.Clients
        .Client(viewerConnectionId)
        .AssistSessionEnded(new(sessionId, reason));
    }

    await _assistHub.Clients
      .Client(session.AssistConnectionId)
      .AssistSessionEnded(new(sessionId, reason));
  }
}
