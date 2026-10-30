using ControlR.Libraries.Api.Contracts.Dtos.HubDtos;
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
    var sessions = await _sessionCache.SnapshotAsync(stoppingToken);

    foreach (var session in sessions)
    {
      stoppingToken.ThrowIfCancellationRequested();
      var age = now - session.StatusChangedAt;

      switch (session.Status)
      {
        case AssistSessionStatus.PendingConsent when age > _consentExpiry:
          {
            var returned = await _sessionCache.TryReturnToWaitingAsync(
              session.SessionId,
              session.ConsentNonce ?? Guid.Empty,
              stoppingToken);
            if (returned is not null &&
                session.ViewerConnectionId is { Length: > 0 } viewerConnectionId)
            {
              await _assistHub.Clients
                .Client(viewerConnectionId)
                .AssistConsentResponded(new(session.SessionId, false, null, AssistConsentRespondedReason.TimedOut));
              await _assistHub.Clients
                .Client(session.AssistConnectionId)
                .AssistConsentTimedOut(new(session.SessionId));
            }
            break;
          }
        case AssistSessionStatus.WaitingForViewer when age > _waitingExpiry:
          {
            if (await _sessionCache.TryRemoveAsync(
                  session.SessionId,
                  AssistSessionStatus.WaitingForViewer,
                  session.StatusChangedAt,
                  stoppingToken) is null)
            {
              break;
            }

            // Tell the assist app its code expired instead of dropping it silently. The app mints
            // a fresh session on the notification so the customer is never left on a dead code.
            await NotifyEnded(session.SessionId, "Your access code expired. Reconnect to get a new one.", session);
            break;
          }
        case AssistSessionStatus.Active when age > _activeExpiry:
          {
            if (await _sessionCache.TryRemoveAsync(
                  session.SessionId,
                  AssistSessionStatus.Active,
                  session.StatusChangedAt,
                  stoppingToken) is null)
            {
              break;
            }

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
