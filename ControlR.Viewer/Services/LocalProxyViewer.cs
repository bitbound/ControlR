using ControlR.Devices.Common.Services.Base;
using ControlR.Shared.Services;
using ControlR.Shared.Services.Buffers;
using Microsoft.Extensions.Logging;

namespace ControlR.Viewer.Services;

public interface ILocalProxyViewer
{
    Task<Result<Task>> ListenForLocalConnections(Guid sessionId, int portNumber);

    Task<Result<Task>> ProxyToLocalService(Guid sessionId, int portNumber);
}

internal class LocalProxyViewer(
    ISettings _settings,
    IAppState _appState,
    IMemoryProvider memoryProvider,
    IRetryer retryer,
    ILogger<TcpWebsocketProxyBase> logger) : TcpWebsocketProxyBase(memoryProvider, retryer, logger), ILocalProxyViewer
{
    public async Task<Result<Task>> ListenForLocalConnections(Guid sessionId, int portNumber)
    {
        var websocketEndpoint = GetWebsocketEndpoint(sessionId);
        return await ListenForLocalConnections(
                sessionId,
                portNumber,
                websocketEndpoint,
                _appState.AppExiting);
    }

    public async Task<Result<Task>> ProxyToLocalService(Guid sessionId, int portNumber)
    {
        var websocketEndpoint = GetWebsocketEndpoint(sessionId);
        return await ProxyToLocalService(
                sessionId,
                portNumber,
                websocketEndpoint,
                _appState.AppExiting);
    }

    private Uri GetWebsocketEndpoint(Guid sessionId)
    {
        var serverOrigin = _settings.ServerUri.TrimEnd('/');
        return new Uri($"{serverOrigin.Replace("http", "ws")}/viewer-proxy/{sessionId}");
    }
}