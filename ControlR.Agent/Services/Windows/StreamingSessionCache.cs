using ControlR.Agent.Models;
using ControlR.Libraries.Shared.Extensions;
using ControlR.Libraries.Shared.Helpers;
using ControlR.Libraries.Shared.Primitives;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace ControlR.Agent.Services.Windows;

internal interface IStreamingSessionCache
{
    IReadOnlyDictionary<int, StreamingSession> Sessions { get; }
    Task AddOrUpdate(StreamingSession session);
    Task KillAllSessions();
    Task<Result<StreamingSession>> TryRemove(int processId);
}
internal class StreamingSessionCache(
    ILogger<StreamingSessionCache> _logger) : IStreamingSessionCache
{
    private readonly ConcurrentDictionary<int, StreamingSession> _sessions = new();

    public IReadOnlyDictionary<int, StreamingSession> Sessions => _sessions;

    public Task AddOrUpdate(StreamingSession session)
    {
        try
        {
            Guard.IsNotNull(session.StreamerProcess);

            _sessions.AddOrUpdate(session.StreamerProcess.Id, session, (_, _) => session);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while adding streaming session to cache.");
        }
        return Task.CompletedTask;
    }

    public async Task KillAllSessions()
    {
        try
        {
            foreach (var key in _sessions.Keys.ToArray())
            {
                if (_sessions.TryRemove(key, out var session))
                {
                    session.Dispose();
                }
                await Task.Yield();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while killing all streaming sessions.");
        }
    }


    public Task<Result<StreamingSession>> TryRemove(int processId)
    {
        try
        {
            if (!_sessions.TryRemove(processId, out var session))
            {
                return Result
                    .Fail<StreamingSession>("Session ID not present in cache.")
                    .Log(_logger)
                    .AsTaskResult();
            }

            return Result.Ok(session).AsTaskResult();
        }
        catch (Exception ex)
        {
            return Result
                .Fail<StreamingSession>(ex, "Error while removing streaming session from cache.")
                .Log(_logger)
                .AsTaskResult();
        }

    }
}
