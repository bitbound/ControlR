using ControlR.Agent.Interfaces;
using ControlR.Agent.Models;
using ControlR.Devices.Common.Services.Base;
using ControlR.Shared.Extensions;
using ControlR.Shared.Services.Buffers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ControlR.Agent.Services;

internal interface ILocalProxy
{
    Task HandleVncSession(VncSession session);
}

internal class LocalProxy(
    IHostApplicationLifetime _appLifetime,
    ISettingsProvider _settings,
    IMemoryProvider _memoryProvider,
    IVncSessionLauncher _vncSessionLauncher,
    ILogger<LocalProxy> logger) : TcpWebsocketProxyBase(_memoryProvider, logger), ILocalProxy
{
    private volatile int _sessionCount;

    public async Task HandleVncSession(VncSession session)
    {
        var sessionId = session.SessionId;
        var serverOrigin = _settings.ServerUri.GetOrigin();
        var websocketEndpoint = new Uri($"{serverOrigin.Replace("http", "ws")}/agent-proxy/{sessionId}");

        var startResult = await StartProxySession(
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
                if (_sessionCount == 0)
                {
                    _logger.LogInformation("All proxy sessions have ended.  Performing cleanup.");
                    await _vncSessionLauncher.CleanupSessions();
                }
            });
        }
    }
}