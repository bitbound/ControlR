using ControlR.Agent.Models;
using ControlR.Libraries.Shared.Helpers;
using ControlR.Libraries.Shared.Primitives;
using Microsoft.Extensions.Hosting;
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
    IRuntimeSettingsProvider _runtimeSettings,
    IHostApplicationLifetime _appLifetime,
    IRegistryAccessor _registryAccessor,
    ILogger<StreamingSessionCache> _logger) : IStreamingSessionCache
{
    private readonly ConcurrentDictionary<int, StreamingSession> _sessions = new();
    private readonly SemaphoreSlim _uacLock = new(1,1);

    public IReadOnlyDictionary<int, StreamingSession> Sessions => _sessions;

    public async Task AddOrUpdate(StreamingSession session)
    {
        await _uacLock.WaitAsync(_appLifetime.ApplicationStopping);
        try
        {
            Guard.IsNotNull(session.StreamerProcess);

            if (session.LowerUacDuringSession && _sessions.IsEmpty)
            {
                var originalPromptValue = _registryAccessor.GetPromptOnSecureDesktop();
                await _runtimeSettings.TrySet(x => x.LowerUacDuringSession = originalPromptValue);
                _registryAccessor.SetPromptOnSecureDesktop(false);
            }

            _sessions.AddOrUpdate(session.StreamerProcess.Id, session, (_, _) => session);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while adding streaming session to cache.");
        }
        finally
        {
            _uacLock.Release();
        }
    }

    public async Task KillAllSessions()
    {
        await _uacLock.WaitAsync(_appLifetime.ApplicationStopping);
        try
        {
            foreach (var key in _sessions.Keys.ToArray())
            {
                if (_sessions.TryRemove(key, out var session))
                {
                    session.Dispose();
                }
            }

            var originalPromptValue = await _runtimeSettings.TryGet(x => x.LowerUacDuringSession);
            if (originalPromptValue.HasValue)
            {
                _registryAccessor.SetPromptOnSecureDesktop(originalPromptValue.Value);
                await _runtimeSettings.TrySet(x => x.LowerUacDuringSession = null);
            }
        }
        finally
        {
            _uacLock.Release();
        }
    }


    public async Task<Result<StreamingSession>> TryRemove(int processId)
    {
        await _uacLock.WaitAsync(_appLifetime.ApplicationStopping);
        try
        {
            if (!_sessions.TryRemove(processId, out var session))
            {
                return Result.Fail<StreamingSession>("Session ID not present in cache.");
            }

            var originalPromptValue = await _runtimeSettings.TryGet(x => x.LowerUacDuringSession);
            if ( _sessions.IsEmpty && originalPromptValue.HasValue)
            {
                _registryAccessor.SetPromptOnSecureDesktop(originalPromptValue.Value);
                await _runtimeSettings.TrySet(x => x.LowerUacDuringSession = null);
            }
            return Result.Ok(session);
        }
        finally
        {
            _uacLock.Release();
        }
    }
}
