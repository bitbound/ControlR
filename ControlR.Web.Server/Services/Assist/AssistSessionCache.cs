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
  string? ViewerEmail = null);

/// <summary>
/// In-memory store for ad-hoc assist sessions. Sessions are ephemeral by design. A server restart
/// clears them, which matches how one-time support codes behave on the products this mirrors.
/// </summary>
public interface IAssistSessionCache
{
  AssistSession Create(string assistConnectionId);
  AssistSession? Remove(Guid sessionId);
  IReadOnlyList<AssistSession> RemoveByAssistConnectionId(string assistConnectionId);
  IReadOnlyList<AssistSession> RemoveByViewerConnectionId(string viewerConnectionId);
  IReadOnlyList<AssistSession> Snapshot();
  bool TryActivate(Guid sessionId, out AssistSession? session);
  bool TryBeginConsent(
    Guid sessionId,
    string viewerConnectionId,
    Guid viewerUserId,
    string displayName,
    string? email,
    out AssistSession? session);
  bool TryGet(Guid sessionId, out AssistSession? session);
  bool TryGetByAssistConnectionId(string assistConnectionId, out AssistSession? session);
  bool TryGetByCode(string code, out AssistSession? session);
  bool TryGetByViewerConnectionId(string viewerConnectionId, out AssistSession? session);
  bool TryReturnToWaiting(Guid sessionId, out AssistSession? session);
}

public class AssistSessionCache(TimeProvider timeProvider) : IAssistSessionCache
{
  public const int CodeLength = 9;

  private const string DigitPool = "0123456789";

  private readonly Dictionary<Guid, AssistSession> _sessions = [];
  private readonly object _sync = new();
  private readonly TimeProvider _timeProvider = timeProvider;

  public AssistSession Create(string assistConnectionId)
  {
    Guard.IsNotNullOrWhiteSpace(assistConnectionId);

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

      var now = _timeProvider.GetUtcNow();
      var session = new AssistSession(
        Guid.NewGuid(),
        GenerateCode(),
        RandomGenerator.CreateAccessToken(),
        assistConnectionId,
        AssistSessionStatus.WaitingForViewer,
        now,
        now);

      _sessions.Add(session.SessionId, session);
      return session;
    }
  }

  public AssistSession? Remove(Guid sessionId)
  {
    lock (_sync)
    {
      if (_sessions.Remove(sessionId, out var session))
      {
        return session;
      }
      return null;
    }
  }

  public IReadOnlyList<AssistSession> RemoveByAssistConnectionId(string assistConnectionId)
  {
    return RemoveWhere(x => x.AssistConnectionId == assistConnectionId);
  }

  public IReadOnlyList<AssistSession> RemoveByViewerConnectionId(string viewerConnectionId)
  {
    return RemoveWhere(x => x.ViewerConnectionId == viewerConnectionId);
  }

  public IReadOnlyList<AssistSession> Snapshot()
  {
    lock (_sync)
    {
      return _sessions.Values.ToList();
    }
  }

  public bool TryActivate(Guid sessionId, out AssistSession? session)
  {
    return TryTransition(
      sessionId,
      AssistSessionStatus.PendingConsent,
      AssistSessionStatus.Active,
      out session);
  }

  public bool TryBeginConsent(
    Guid sessionId,
    string viewerConnectionId,
    Guid viewerUserId,
    string displayName,
    string? email,
    out AssistSession? session)
  {
    lock (_sync)
    {
      if (!_sessions.TryGetValue(sessionId, out var existing) ||
          existing.Status != AssistSessionStatus.WaitingForViewer)
      {
        session = null;
        return false;
      }

      session = existing with
      {
        Status = AssistSessionStatus.PendingConsent,
        StatusChangedAt = _timeProvider.GetUtcNow(),
        ViewerConnectionId = viewerConnectionId,
        ViewerUserId = viewerUserId.ToString(),
        ViewerDisplayName = displayName,
        ViewerEmail = email,
      };

      _sessions[sessionId] = session;
      return true;
    }
  }

  public bool TryGet(Guid sessionId, out AssistSession? session)
  {
    lock (_sync)
    {
      return _sessions.TryGetValue(sessionId, out session);
    }
  }

  public bool TryGetByAssistConnectionId(string assistConnectionId, out AssistSession? session)
  {
    return TryGetFirst(x => x.AssistConnectionId == assistConnectionId, out session);
  }

  public bool TryGetByCode(string code, out AssistSession? session)
  {
    return TryGetFirst(x => x.Code == code, out session);
  }

  public bool TryGetByViewerConnectionId(string viewerConnectionId, out AssistSession? session)
  {
    return TryGetFirst(x => x.ViewerConnectionId == viewerConnectionId, out session);
  }

  public bool TryReturnToWaiting(Guid sessionId, out AssistSession? session)
  {
    lock (_sync)
    {
      if (!_sessions.TryGetValue(sessionId, out var existing) ||
          existing.Status != AssistSessionStatus.PendingConsent)
      {
        session = null;
        return false;
      }

      session = existing with
      {
        Status = AssistSessionStatus.WaitingForViewer,
        StatusChangedAt = _timeProvider.GetUtcNow(),
        ViewerConnectionId = null,
        ViewerUserId = null,
        ViewerDisplayName = null,
        ViewerEmail = null,
      };

      _sessions[sessionId] = session;
      return true;
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

  private IReadOnlyList<AssistSession> RemoveWhere(Func<AssistSession, bool> predicate)
  {
    lock (_sync)
    {
      var removed = _sessions.Values.Where(predicate).ToList();
      foreach (var session in removed)
      {
        _sessions.Remove(session.SessionId);
      }
      return removed;
    }
  }

  private bool TryGetFirst(Func<AssistSession, bool> predicate, out AssistSession? session)
  {
    lock (_sync)
    {
      session = _sessions.Values.FirstOrDefault(predicate);
      return session is not null;
    }
  }

  private bool TryTransition(Guid sessionId, AssistSessionStatus from, AssistSessionStatus to, out AssistSession? session)
  {
    lock (_sync)
    {
      if (!_sessions.TryGetValue(sessionId, out var existing) || existing.Status != from)
      {
        session = null;
        return false;
      }

      session = existing with
      {
        Status = to,
        StatusChangedAt = _timeProvider.GetUtcNow(),
      };

      _sessions[sessionId] = session;
      return true;
    }
  }
}
