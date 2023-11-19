using Bitbound.SimpleMessenger;
using ControlR.Devices.Common.Extensions;
using ControlR.Devices.Common.Messages;
using ControlR.Shared.Helpers;
using ControlR.Shared.Primitives;
using ControlR.Shared.Services.Buffers;
using ControlR.Viewer.Models.Messages;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;

namespace ControlR.Devices.Common.Services.Base;

public abstract class TcpWebsocketProxyBase(
    IMemoryProvider memoryProvider,
    IMessenger messenger,
    ILogger<TcpWebsocketProxyBase> logger)
{
    protected readonly ILogger<TcpWebsocketProxyBase> _logger = logger;
    private readonly IMemoryProvider _memoryProvider = memoryProvider;
    private readonly IMessenger _messenger = messenger;

    protected async Task<Result<Task>> ListenForLocalConnections(
       Guid sessionId,
       int listeningPort,
       Uri websocketUri,
       CancellationToken cancellationToken)
    {
        var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        _messenger.RegisterGenericMessage(linkedSource, HandleGenericMessage);
        _messenger.Register<ProxyListenerStatusChangedMessage>(linkedSource, HandleProxyListenerStatusChangedMessage);

        var ws = new ClientWebSocket();
        TcpListener? tcpListener = null;

        try
        {
            _logger.LogInformation("Starting proxy for session ID {SessionID}.  Listening for connections on port {ListeningPort}.",
                sessionId,
                listeningPort);

            tcpListener = new TcpListener(IPAddress.Loopback, listeningPort);

            TryHelper.Retry(
                () =>
                {
                    tcpListener.Start();
                },
                4,
                TimeSpan.FromSeconds(1));

            await ws.ConnectAsync(websocketUri, linkedSource.Token);

            var listenerTask = Task.Run(async () =>
            {
                try
                {
                    var client = await tcpListener.AcceptTcpClientAsync(linkedSource.Token);
                    await ProxyConnections(sessionId, ws, client, linkedSource.Token);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("TCP listening task cancelled.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while listening for TCP client to proxy.");
                }
                finally
                {
                    _logger.LogInformation("Disposing TCP listener.");
                    tcpListener.Dispose();
                    _messenger.SendGenericMessage(GenericMessageKind.LocalProxyListenerStopRequested);
                }
            }, linkedSource.Token);

            return Result.Ok(listenerTask);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("TCP listening task cancelled unexpectedly.");
            return Result.Fail<Task>("Listening task cancelled unexpectedly.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while proxying stream.");
            DisposeHelper.DisposeAll(ws, tcpListener);
            return Result.Fail<Task>("Error while creating proxy session.");
        }
    }

    protected async Task<Result<Task>> ProxyToLocalService(
        Guid sessionId,
        int localServicePort,
        Uri websocketUri,
        CancellationToken cancellationToken)
    {
        var ws = new ClientWebSocket();

        try
        {
            _logger.LogInformation("Starting proxy for session ID {SessionID} to local service port {LocalServicePort}.",
                sessionId,
                localServicePort);

            var tcpClient = new TcpClient();
            await TryHelper.Retry(
                async () =>
                {
                    await tcpClient.ConnectAsync("127.0.0.1", localServicePort);
                },
                tryCount: 3,
                retryDelay: TimeSpan.FromSeconds(3));

            await ws.ConnectAsync(websocketUri, cancellationToken);

            var proxyTask = ProxyConnections(sessionId, ws, tcpClient, cancellationToken);
            return Result.Ok(proxyTask);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while proxying stream.");
            ws.Dispose();
            return Result.Fail<Task>("Error while creating proxy session.");
        }
    }

    private void HandleGenericMessage(object subscriber, GenericMessageKind kind)
    {
        if (kind == GenericMessageKind.LocalProxyListenerStopRequested)
        {
            if (subscriber is CancellationTokenSource cts)
            {
                cts.Cancel();
                cts.Dispose();
            }
        }
    }

    private Task HandleProxyListenerStatusChangedMessage(object subscriber, ProxyListenerStatusChangedMessage message)
    {
        if (!message.IsRunning &&
            subscriber is CancellationTokenSource cts)
        {
            cts.Cancel();
            cts.Dispose();
        }
        return Task.CompletedTask;
    }
    private async Task ProxyConnections(
         Guid sessionId,
         WebSocket serverConnection,
         TcpClient localConnection,
         CancellationToken cancellationToken)
    {
        try
        {
            var localReadTask = ReadFromLocal(localConnection, serverConnection, cancellationToken);
            var serverReadTask = ReadFromServer(serverConnection, localConnection, cancellationToken);

            await Task.WhenAny(localReadTask, serverReadTask);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while proxying connections.");
        }
        finally
        {
            _logger.LogInformation("Proxy session ended.  Session ID: {SessionID}", sessionId);
            DisposeHelper.DisposeAll(serverConnection, localConnection);
        }
    }

    private async Task ReadFromLocal(
        TcpClient localConnection,
        WebSocket serverConnection,
        CancellationToken cancellationToken)
    {
        using var buffer = _memoryProvider.CreateEphemeralBuffer<byte>(ushort.MaxValue);

        try
        {
            while (localConnection.Connected &&
                serverConnection.State == WebSocketState.Open &&
                !cancellationToken.IsCancellationRequested)
            {
                var bytesReceived = await localConnection.Client.ReceiveAsync(buffer.Value, cancellationToken);
                if (bytesReceived == 0)
                {
                    continue;
                }

                await serverConnection.SendAsync(
                    buffer.Value.AsMemory()[0..bytesReceived],
                    WebSocketMessageType.Binary,
                    true,
                    cancellationToken);
            }
        }
        catch (SocketException ex) when (ex.SocketErrorCode is
           SocketError.OperationAborted or
           SocketError.ConnectionAborted)
        {
            _logger.LogInformation("Socket connection aborted.");
        }
        catch (WebSocketException ex) when (ex.WebSocketErrorCode is WebSocketError.ConnectionClosedPrematurely)
        {
            _logger.LogInformation("Websocket connection closed.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while reading from local connection.");
        }
        finally
        {
            _logger.LogInformation("Read from local connection ended.");
        }
    }

    private async Task ReadFromServer(
        WebSocket serverConnection,
        TcpClient localConnection,
        CancellationToken cancellationToken)
    {
        using var buffer = _memoryProvider.CreateEphemeralBuffer<byte>(ushort.MaxValue);

        try
        {
            while (localConnection.Connected &&
              serverConnection.State == WebSocketState.Open &&
              !cancellationToken.IsCancellationRequested)
            {
                var result = await serverConnection.ReceiveAsync(buffer.Value, cancellationToken);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    _logger.LogInformation("Websocket close message received.");
                    break;
                }

                if (result.Count == 0)
                {
                    continue;
                }

                await localConnection.Client.SendAsync(
                    buffer.Value.AsMemory()[0..result.Count],
                    cancellationToken);
            }
        }
        catch (SocketException ex) when (ex.SocketErrorCode is
            SocketError.OperationAborted or
            SocketError.ConnectionAborted)
        {
            _logger.LogInformation("Socket connection aborted.");
        }
        catch (WebSocketException ex) when (ex.WebSocketErrorCode is WebSocketError.ConnectionClosedPrematurely)
        {
            _logger.LogInformation("Websocket connection closed.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while reading from server connection.");
        }
        finally
        {
            _logger.LogInformation("Read from server ended.");
        }
    }
}