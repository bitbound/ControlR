using Bitbound.SimpleMessenger;
using ControlR.Devices.Common.Services.Base;
using ControlR.Shared.Services.Buffers;
using ControlR.Viewer.Models.Messages;
using Microsoft.Extensions.Logging;

namespace ControlR.Viewer.Services;

public interface ILocalProxyViewer
{
    Task<Result> ListenForLocalConnections(Guid sessionId, int portNumber);

    Task<Result> ProxyToLocalService(Guid sessionId, int portNumber);
}

internal class LocalProxyViewer : TcpWebsocketProxyBase, ILocalProxyViewer
{
    private readonly IAppState _appState;
    private readonly ISettings _settings;
    private CancellationTokenSource _linkedCancellationSource = new();
    private CancellationTokenSource _proxyCancellationSource = new();

    public LocalProxyViewer(
        ISettings settings,
        IMemoryProvider memoryProvider,
        IAppState appState,
        IMessenger messenger,
        ILogger<TcpWebsocketProxyBase> logger)
        : base(memoryProvider, messenger, logger)
    {
        _settings = settings;
        _appState = appState;

        messenger.Register<LocalProxyStatusChanged>(this, HandleLocalProxyStatusChangedMessage);
    }

    public async Task<Result> ListenForLocalConnections(Guid sessionId, int portNumber)
    {
        return await StartProxySession(sessionId, portNumber, true);
    }

    public async Task<Result> ProxyToLocalService(Guid sessionId, int portNumber)
    {
        return await StartProxySession(sessionId, portNumber, false);
    }

    private void CancelAndDisposeSources()
    {
        try
        {
            _proxyCancellationSource.Cancel();
            _proxyCancellationSource.Dispose();
        }
        catch { }
        try
        {
            _linkedCancellationSource.Cancel();
            _linkedCancellationSource.Dispose();
        }
        catch { }
    }

    private CancellationToken GetNewListenerCancellationToken()
    {
        CancelAndDisposeSources();

        _proxyCancellationSource = new CancellationTokenSource();
        _linkedCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(_proxyCancellationSource.Token, _appState.AppExiting);
        return _linkedCancellationSource.Token;
    }

    private Task HandleLocalProxyStatusChangedMessage(LocalProxyStatusChanged message)
    {
        if (!message.IsRunning)
        {
            CancelAndDisposeSources();
        }
        return Task.CompletedTask;
    }

    private async Task<Result> StartProxySession(Guid sessionId, int portNumber, bool isListener)
    {
        var listenerToken = GetNewListenerCancellationToken();

        var serverOrigin = _settings.ServerUri.TrimEnd('/');
        var websocketEndpoint = new Uri($"{serverOrigin.Replace("http", "ws")}/viewer-proxy/{sessionId}");

        var startResult =
            isListener ?

            await ListenForLocalConnections(
                sessionId,
                portNumber,
                websocketEndpoint,
                listenerToken) :

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