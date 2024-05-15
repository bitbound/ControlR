using ControlR.Agent.Models;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace ControlR.Agent.Services.Windows;

internal interface IStreamingSessionCache
{
    IReadOnlyDictionary<Guid, StreamingSession> Sessions { get; }
    void AddOrUpdate(StreamingSession session);
    bool TryRemove(Guid sessionId, [NotNullWhen(true)] out StreamingSession? session);
}
internal class StreamingSessionCache(IRegistryAccessor _registryAccessor) : IStreamingSessionCache
{
    private readonly ConcurrentDictionary<Guid, StreamingSession> _sessions = new();
    private readonly object _uacLock = new();
    private bool _originalPromptValue = true;

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

    public bool TryRemove(Guid sessionId, [NotNullWhen(true)] out StreamingSession? session)
    {
        lock (_uacLock)
        {
            {
                var result = _sessions.TryRemove(sessionId, out session);
                if (result && _sessions.IsEmpty && session?.LowerUacDuringSession == true)
                {
                    _registryAccessor.SetPromptOnSecureDesktop(_originalPromptValue);
                }
                return result;
            }
        }
    }
}
