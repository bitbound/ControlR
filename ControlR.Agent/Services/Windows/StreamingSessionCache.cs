using ControlR.Agent.Models;
using System.Collections.Concurrent;

namespace ControlR.Agent.Services.Windows;

internal interface IStreamingSessionCache
{
    ConcurrentDictionary<Guid, StreamingSession> Sessions { get; }
}
internal class StreamingSessionCache : IStreamingSessionCache
{
    public ConcurrentDictionary<Guid, StreamingSession> Sessions { get; } = new();

}
