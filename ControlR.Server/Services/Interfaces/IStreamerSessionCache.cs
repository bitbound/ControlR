using ControlR.Shared.Models;
using ControlR.Shared.Primitives;
using System.Diagnostics.CodeAnalysis;

namespace ControlR.Server.Services.Interfaces;

public interface IStreamerSessionCache
{
    Task<Result<StreamerHubSession[]>> GetAllSessions();
    Task<Result> AddOrUpdate(Guid sessionId, StreamerHubSession streamerHubSession);

    Task<Result<StreamerHubSession>> TryGetValue(Guid sessionId);
    Task<Result<StreamerHubSession>> TryRemove(Guid sessionId);
}
