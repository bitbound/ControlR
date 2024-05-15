using ControlR.Agent.Models;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace ControlR.Agent.Services.Windows;

internal interface IStreamingSessionCache
{
    IReadOnlyDictionary<Guid, StreamingSession> Sessions { get; }
    void AddOrUpdate(StreamingSession session);
    void KillAllSessions();
    bool TryRemove(Guid sessionId, [NotNullWhen(true)] out StreamingSession? session);
}
internal class StreamingSessionCache(IRegistryAccessor _registryAccessor) : IStreamingSessionCache
{
    private readonly ConcurrentDictionary<Guid, StreamingSession> _sessions = new();
    private readonly object _uacLock = new();
    private bool? _originalPromptValue;

    public IReadOnlyDictionary<Guid, StreamingSession> Sessions => _sessions;

    public void AddOrUpdate(StreamingSession session)
    {
        lock (_uacLock)
        {
            if (session.LowerUacDuringSession && _sessions.IsEmpty)
            {
                _originalPromptValue = _registryAccessor.GetPromptOnSecureDesktop();
                _registryAccessor.SetPromptOnSecureDesktop(false);
            }

            _sessions.AddOrUpdate(session.SessionId, session, (_, _) => session);
        }

    }

    public void KillAllSessions()
    {
        lock (_uacLock)
        {
            foreach (var key in _sessions.Keys.ToArray())
            {
                if (_sessions.TryRemove(key, out var session))
                {
                    session.Dispose();
                }
            }
            if (_originalPromptValue.HasValue)
            {
                _registryAccessor.SetPromptOnSecureDesktop(_originalPromptValue.Value);
                _originalPromptValue = null;
            }
        }
    }


    public bool TryRemove(Guid sessionId, [NotNullWhen(true)] out StreamingSession? session)
    {
        lock (_uacLock)
        {
            {
                var result = _sessions.TryRemove(sessionId, out session);
                if (result && _sessions.IsEmpty && _originalPromptValue.HasValue)
                {
                    _registryAccessor.SetPromptOnSecureDesktop(_originalPromptValue.Value);
                    _originalPromptValue = null;
                }
                return result;
            }
        }
    }
}
