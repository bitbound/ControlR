using Bitbound.SimpleMessenger;
using ControlR.Devices.Common.Extensions;
using ControlR.Devices.Common.Messages;
using ControlR.Devices.Common.Services.Base;
using ControlR.Shared.Services.Buffers;
using Microsoft.Extensions.Logging;

namespace ControlR.Viewer.Services;

public interface ILocalProxyViewer
{
    Task<Result> ListenForLocalConnections(Guid sessionId, int portNumber);

    Task<Result> ProxyToLocalService(Guid sessionId, int portNumber);
}

internal class LocalProxyViewer(
    ISettings settings,
    IMemoryProvider memoryProvider,
    IAppState appState,
    IMessenger messenger,
    ILogger<TcpWebsocketProxyBase> logger) : TcpWebsocketProxyBase(memoryProvider, messenger, logger), ILocalProxyViewer
{
    private readonly IAppState _appState = appState;
    private readonly ISettings _settings = settings;

    public async Task<Result> ListenForLocalConnections(Guid sessionId, int portNumber)
    {
        return await StartProxySession(sessionId, portNumber, true);
    }

    public async Task<Result> ProxyToLocalService(Guid sessionId, int portNumber)
    {
        return await StartProxySession(sessionId, portNumber, false);
    }

    private async Task<Result> StartProxySession(Guid sessionId, int portNumber, bool isListener)
    {
        var serverOrigin = _settings.ServerUri.TrimEnd('/');
        var websocketEndpoint = new Uri($"{serverOrigin.Replace("http", "ws")}/viewer-proxy/{sessionId}");

        var startResult =
            isListener ?

            await ListenForLocalConnections(
                sessionId,
                portNumber,
                websocketEndpoint,
                _appState.AppExiting) :

            await ProxyToLocalService(
                sessionId,
                portNumber,
                websocketEndpoint,
                _appState.AppExiting);

        if (startResult.IsSuccess)
        {
            return Result.Ok();
        }
        else
        {
            return startResult.ToResult();
        }
    }
}