using ControlR.Shared.Models;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace ControlR.Server.Services.Interfaces;

public interface IStreamerSessionCache
{
    ConcurrentDictionary<Guid, StreamerHubSession> Sessions { get; }
    void AddOrUpdate(Guid sessionId, StreamerHubSession streamerHubSession);

    bool TryGetValue(Guid sessionId, [NotNullWhen(true)] out StreamerHubSession? session);
    bool TryRemove(Guid sessionId, [NotNullWhen(true)] out StreamerHubSession? session);
}
