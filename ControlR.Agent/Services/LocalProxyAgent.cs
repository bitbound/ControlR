using Bitbound.SimpleMessenger;
using ControlR.Agent.Interfaces;
using ControlR.Agent.Models;
using ControlR.Devices.Common.Services.Base;
using ControlR.Shared.Extensions;
using ControlR.Shared.Primitives;
using ControlR.Shared.Services;
using ControlR.Shared.Services.Buffers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ControlR.Agent.Services;

internal interface ILocalProxyAgent
{
    Task HandleVncSession(VncSession session);

    Task<Result<Task>> ListenForLocalConnections(Guid sessionId, int portNumber);

    Task<Result<Task>> ProxyToLocalService(Guid sessionId, int portNumber);
}

internal class LocalProxyAgent(
    IHostApplicationLifetime _appLifetime,
    ISettingsProvider _settings,
    IMemoryProvider _memoryProvider,
    IVncSessionLauncher _vncSessionLauncher,
    IMessenger _messenger,
    IRetryer _retryer,
    ILogger<LocalProxyAgent> logger) : TcpWebsocketProxyBase(_memoryProvider, _messenger, _retryer, logger), ILocalProxyAgent
{
    private volatile int _sessionCount;

    public async Task HandleVncSession(VncSession session)
    {
        var sessionId = session.SessionId;
        var serverOrigin = _settings.ServerUri.GetOrigin();
        var websocketEndpoint = new Uri($"{serverOrigin.Replace("http", "ws")}/agent-proxy/{sessionId}");

        var startResult = await ProxyToLocalService(
            sessionId,
            _settings.VncPort,
            websocketEndpoint,
            _appLifetime.ApplicationStopping);

        if (startResult.IsSuccess)
        {
            Interlocked.Increment(ref _sessionCount);

            _ = startResult.Value.ContinueWith(async x =>
            {
                Interlocked.Decrement(ref _sessionCount);
                if (_sessionCount == 0 && session.AutoRunUsed)
                {
                    _logger.LogInformation("All proxy sessions have ended.  Performing cleanup.");
                    await _vncSessionLauncher.CleanupSessions();
                }
            });
        }
    }

    public async Task<Result<Task>> ListenForLocalConnections(Guid sessionId, int portNumber)
    {
        var websocketEndpoint = GetWebsocketEndpoint(sessionId);
        return await ListenForLocalConnections(
                sessionId,
                portNumber,
                websocketEndpoint,
                _appLifetime.ApplicationStopping);
    }

    public async Task<Result<Task>> ProxyToLocalService(Guid sessionId, int portNumber)
    {
        var websocketEndpoint = GetWebsocketEndpoint(sessionId);
        return await ProxyToLocalService(
              sessionId,
              portNumber,
              websocketEndpoint,
              _appLifetime.ApplicationStopping);
    }

    private Uri GetWebsocketEndpoint(Guid sessionId)
    {
        var serverOrigin = _settings.ServerUri.GetOrigin();
        return new Uri($"{serverOrigin.Replace("http", "ws")}/agent-proxy/{sessionId}");
    }
}