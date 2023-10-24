using ControlR.Server.Models;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace ControlR.Server.Services;

public interface IProxyStreamStore
{
    void AddOrUpdate(Guid sessionId, StreamSignaler signaler, Func<Guid, StreamSignaler, StreamSignaler> updateFactory);

    bool Exists(Guid sessionId);

    StreamSignaler GetOrAdd(Guid sessionId, Func<Guid, StreamSignaler> createFactory);

    bool TryGet(Guid sessionId, [NotNullWhen(true)] out StreamSignaler? signaler);

    bool TryRemove(Guid sessionId, [NotNullWhen(true)] out StreamSignaler? signaler);
}

public class ProxyStreamStore(ILogger<ProxyStreamStore> logger) : IProxyStreamStore
{
    private readonly ILogger<ProxyStreamStore> _logger = logger;
    private readonly ConcurrentDictionary<Guid, StreamSignaler> _proxyStreams = new();

    public void AddOrUpdate(Guid sessionId, StreamSignaler signaler, Func<Guid, StreamSignaler, StreamSignaler> updateFactory)
    {
        _proxyStreams.AddOrUpdate(sessionId, signaler, updateFactory);
    }

    public bool Exists(Guid sessionId)
    {
        return _proxyStreams.ContainsKey(sessionId);
    }

    public StreamSignaler GetOrAdd(Guid sessionId, Func<Guid, StreamSignaler> createFactory)
    {
        return _proxyStreams.GetOrAdd(sessionId, createFactory);
    }

    public bool TryGet(Guid sessionId, [NotNullWhen(true)] out StreamSignaler? signaler)
    {
        return _proxyStreams.TryGetValue(sessionId, out signaler);
    }

    public bool TryRemove(Guid sessionId, [NotNullWhen(true)] out StreamSignaler? signaler)
    {
        return _proxyStreams.TryRemove(sessionId, out signaler);
    }
}