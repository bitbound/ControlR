using System.Security.Cryptography;
using ControlR.Libraries.Shared.Helpers;

namespace ControlR.Web.Server.Services.Assist;

public enum AssistSessionStatus
{
  WaitingForViewer,
  PendingConsent,
  Active,
}

public sealed record AssistSession(
  Guid SessionId,
  string Code,
  string AccessToken,
  string AssistConnectionId,
  AssistSessionStatus Status,
  DateTimeOffset CreatedAt,
  DateTimeOffset StatusChangedAt,
  string? ViewerConnectionId = null,
  string? ViewerUserId = null,
  string? ViewerDisplayName = null,
  string? ViewerEmail = null,
  Guid? ConsentNonce = null);

/// <summary>
/// Thrown by <see cref="IAssistSessionCache.CreateAsync"/> when the global session cap is hit.
/// Session creation is anonymous, so the cap is the backpressure against memory exhaustion.
/// </summary>
public sealed class AssistSessionCapacityExceededException : Exception
{
  public AssistSessionCapacityExceededException()
    : base("The assist session capacity has been reached.")
  {
  }
}

/// <summary>
/// Store for ad-hoc assist sessions. Sessions are ephemeral by design. A server restart clears
/// them, which matches how one-time support codes behave on the products this mirrors. The async
/// surface is deliberate so a distributed implementation can swap in without touching callers.
/// </summary>
public interface IAssistSessionCache
{
  Task<AssistSession> CreateAsync(string assistConnectionId, CancellationToken cancellationToken = default);

  Task<AssistSession?> RemoveAsync(Guid sessionId, CancellationToken cancellationToken = default);

  Task<IReadOnlyList<AssistSession>> RemoveByAssistConnectionIdAsync(
    string assistConnectionId,
    CancellationToken cancellationToken = default);

  Task<IReadOnlyList<AssistSession>> SnapshotAsync(CancellationToken cancellationToken = default);

  Task<AssistSession?> TryActivateAsync(
    Guid sessionId,
    Guid consentNonce,
    CancellationToken cancellationToken = default);

  Task<AssistSession?> TryBeginConsentAsync(
    Guid sessionId,
    string viewerConnectionId,
    Guid viewerUserId,
    string displayName,
    string? email,
    CancellationToken cancellationToken = default);
    
  Task<AssistSession?> TryGetAsync(Guid sessionId, CancellationToken cancellationToken = default);

  Task<AssistSession?> TryGetByAssistConnectionIdAsync(
    string assistConnectionId,
    CancellationToken cancellationToken = default);

  Task<AssistSession?> TryGetByCodeAsync(string code, CancellationToken cancellationToken = default);

  /// <summary>
  /// Removes the session only if it still matches the given status and status timestamp. Guards
  /// cleanup paths that decided from a stale snapshot against deleting a since-transitioned session.
  /// </summary>
  Task<AssistSession?> TryRemoveAsync(
    Guid sessionId,
    AssistSessionStatus expectedStatus,
    DateTimeOffset expectedStatusChangedAt,
    CancellationToken cancellationToken = default);

  Task<AssistSession?> TryReturnToWaitingAsync(
    Guid sessionId,
    Guid consentNonce,
    CancellationToken cancellationToken = default);
}

/// <summary>
/// <para>
/// In-memory <see cref="IAssistSessionCache"/>. The lock serializes state transitions within this
/// process only.
/// </para>
/// <para>
/// TODO: HybridCache. Move this state behind HybridCache when Redis and horizontal scalability
/// land. The lock will not serialize sessions across nodes, and the code-uniqueness lookup in
/// </para>
/// <see cref="CreateAsync"/> will need to pair with the distributed lock work to be race-free.
/// </summary>
public sealed class AssistSessionCache(TimeProvider timeProvider, Func<string>? codeGenerator = null) : IAssistSessionCache
{
  public const int CodeLength = 9;
  public const int MaxTrackedSessions = 10_000;

  private const string DigitPool = "0123456789";

  private readonly Func<string> _codeGenerator = codeGenerator ?? GenerateCode;
  private readonly Dictionary<Guid, AssistSession> _sessions = [];
  private readonly object _sync = new();
  private readonly TimeProvider _timeProvider = timeProvider;

  public Task<AssistSession> CreateAsync(string assistConnectionId, CancellationToken cancellationToken = default)
  {
    Guard.IsNotNullOrWhiteSpace(assistConnectionId);
    cancellationToken.ThrowIfCancellationRequested();

    lock (_sync)
    {
      var stale = _sessions.Values
        .Where(x => x.AssistConnectionId == assistConnectionId)
        .Select(x => x.SessionId)
        .ToList();

      foreach (var sessionId in stale)
      {
        _sessions.Remove(sessionId);
      }

      if (_sessions.Count >= MaxTrackedSessions)
      {
        throw new AssistSessionCapacityExceededException();
      }

      var now = _timeProvider.GetUtcNow();
      string code;
      do
      {
        code = _codeGenerator();
      }
      while (_sessions.Values.Any(x => x.Code == code));

      var session = new AssistSession(
        Guid.NewGuid(),
        code,
        RandomGenerator.CreateAccessToken(),
        assistConnectionId,
        AssistSessionStatus.WaitingForViewer,
        now,
        now);

      _sessions.Add(session.SessionId, session);
      return Task.FromResult(session);
    }
  }

  public Task<AssistSession?> RemoveAsync(Guid sessionId, CancellationToken cancellationToken = default)
  {
    cancellationToken.ThrowIfCancellationRequested();

    lock (_sync)
    {
      return Task.FromResult(_sessions.Remove(sessionId, out var session) ? session : null);
    }
  }

  public Task<IReadOnlyList<AssistSession>> RemoveByAssistConnectionIdAsync(
    string assistConnectionId,
    CancellationToken cancellationToken = default)
  {
    return RemoveWhereAsync(x => x.AssistConnectionId == assistConnectionId, cancellationToken);
  }

  public Task<IReadOnlyList<AssistSession>> SnapshotAsync(CancellationToken cancellationToken = default)
  {
    cancellationToken.ThrowIfCancellationRequested();

    lock (_sync)
    {
      return Task.FromResult<IReadOnlyList<AssistSession>>(_sessions.Values.ToList());
    }
  }

  public Task<AssistSession?> TryActivateAsync(
    Guid sessionId,
    Guid consentNonce,
    CancellationToken cancellationToken = default)
  {
    return TryTransitionAsync(
      sessionId,
      AssistSessionStatus.PendingConsent,
      AssistSessionStatus.Active,
      expectedConsentNonce: consentNonce,
      cancellationToken);
  }

  public Task<AssistSession?> TryBeginConsentAsync(
    Guid sessionId,
    string viewerConnectionId,
    Guid viewerUserId,
    string displayName,
    string? email,
    CancellationToken cancellationToken = default)
  {
    cancellationToken.ThrowIfCancellationRequested();

    lock (_sync)
    {
      if (!_sessions.TryGetValue(sessionId, out var existing) ||
          existing.Status != AssistSessionStatus.WaitingForViewer)
      {
        return Task.FromResult<AssistSession?>(null);
      }

      var session = existing with
      {
        Status = AssistSessionStatus.PendingConsent,
        StatusChangedAt = _timeProvider.GetUtcNow(),
        ViewerConnectionId = viewerConnectionId,
        ViewerUserId = viewerUserId.ToString(),
        ViewerDisplayName = displayName,
        ViewerEmail = email,
        ConsentNonce = Guid.NewGuid(),
      };

      _sessions[sessionId] = session;
      return Task.FromResult<AssistSession?>(session);
    }
  }

  public Task<AssistSession?> TryGetAsync(Guid sessionId, CancellationToken cancellationToken = default)
  {
    cancellationToken.ThrowIfCancellationRequested();

    lock (_sync)
    {
      return Task.FromResult<AssistSession?>(_sessions.TryGetValue(sessionId, out var session) ? session : null);
    }
  }

  public Task<AssistSession?> TryGetByAssistConnectionIdAsync(
    string assistConnectionId,
    CancellationToken cancellationToken = default)
  {
    return TryGetFirstAsync(x => x.AssistConnectionId == assistConnectionId, cancellationToken);
  }

  public Task<AssistSession?> TryGetByCodeAsync(string code, CancellationToken cancellationToken = default)
  {
    return TryGetFirstAsync(x => x.Code == code, cancellationToken);
  }

  public Task<AssistSession?> TryRemoveAsync(
    Guid sessionId,
    AssistSessionStatus expectedStatus,
    DateTimeOffset expectedStatusChangedAt,
    CancellationToken cancellationToken = default)
  {
    cancellationToken.ThrowIfCancellationRequested();

    lock (_sync)
    {
      if (!_sessions.TryGetValue(sessionId, out var session) ||
          session.Status != expectedStatus ||
          session.StatusChangedAt != expectedStatusChangedAt)
      {
        return Task.FromResult<AssistSession?>(null);
      }

      _sessions.Remove(sessionId);
      return Task.FromResult<AssistSession?>(session);
    }
  }

  public Task<AssistSession?> TryReturnToWaitingAsync(
    Guid sessionId,
    Guid consentNonce,
    CancellationToken cancellationToken = default)
  {
    cancellationToken.ThrowIfCancellationRequested();

    lock (_sync)
    {
      if (!_sessions.TryGetValue(sessionId, out var existing) ||
          existing.Status != AssistSessionStatus.PendingConsent ||
          existing.ConsentNonce != consentNonce)
      {
        return Task.FromResult<AssistSession?>(null);
      }

      var session = existing with
      {
        Status = AssistSessionStatus.WaitingForViewer,
        StatusChangedAt = _timeProvider.GetUtcNow(),
        ViewerConnectionId = null,
        ViewerUserId = null,
        ViewerDisplayName = null,
        ViewerEmail = null,
        ConsentNonce = null,
      };

      _sessions[sessionId] = session;
      return Task.FromResult<AssistSession?>(session);
    }
  }

  private static string GenerateCode()
  {
    var chars = new char[CodeLength];
    for (var i = 0; i < CodeLength; i++)
    {
      chars[i] = DigitPool[RandomNumberGenerator.GetInt32(DigitPool.Length)];
    }

    return new string(chars);
  }

  private Task<IReadOnlyList<AssistSession>> RemoveWhereAsync(
    Func<AssistSession, bool> predicate,
    CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();

    lock (_sync)
    {
      var removed = _sessions.Values.Where(predicate).ToList();
      foreach (var session in removed)
      {
        _sessions.Remove(session.SessionId);
      }

      return Task.FromResult<IReadOnlyList<AssistSession>>(removed);
    }
  }

  private Task<AssistSession?> TryGetFirstAsync(Func<AssistSession, bool> predicate, CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();

    lock (_sync)
    {
      return Task.FromResult(_sessions.Values.FirstOrDefault(predicate));
    }
  }

  private Task<AssistSession?> TryTransitionAsync(
    Guid sessionId,
    AssistSessionStatus from,
    AssistSessionStatus to,
    Guid? expectedConsentNonce,
    CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();

    lock (_sync)
    {
      if (!_sessions.TryGetValue(sessionId, out var existing) ||
          existing.Status != from ||
          (expectedConsentNonce is { } nonce && existing.ConsentNonce != nonce))
      {
        return Task.FromResult<AssistSession?>(null);
      }

      var session = existing with
      {
        Status = to,
        StatusChangedAt = _timeProvider.GetUtcNow(),
        ConsentNonce = null,
      };

      _sessions[sessionId] = session;
      return Task.FromResult<AssistSession?>(session);
    }
  }
}
