using ControlR.Agent.Models;
using System.Collections.Concurrent;

namespace ControlR.Agent.Services.Windows;

internal interface IVncProcessCache
{
    ConcurrentDictionary<Guid, VncSession> Sessions { get; }
}
internal class StreamingSessionCache : IVncProcessCache
{
    public ConcurrentDictionary<Guid, VncSession> Sessions { get; } = new();

}
