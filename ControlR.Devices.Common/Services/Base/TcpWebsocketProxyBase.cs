using Bitbound.SimpleMessenger;
using ControlR.Shared.Extensions;
using ControlR.Shared.Helpers;
using ControlR.Shared.Primitives;
using ControlR.Shared.Services;
using ControlR.Shared.Services.Buffers;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;

namespace ControlR.Devices.Common.Services.Base;

public abstract class TcpWebsocketProxyBase(
    IMemoryProvider _memoryProvider,
    IMessenger _messenger,
    IRetryer _retryer,
    ILogger<TcpWebsocketProxyBase> _logger)
{
    protected readonly ILogger<TcpWebsocketProxyBase> _logger = _logger;
    private readonly ConcurrentDictionary<int, TcpListener> _listeners = [];
    private readonly IMemoryProvider _memoryProvider = _memoryProvider;
    private readonly IMessenger _messenger = _messenger;

    protected Task<Result<Task>> ListenForLocalConnections(
       Guid sessionId,
       int listeningPort,
       Uri websocketUri,
       CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Starting proxy for session ID {SessionID}, listening port {ListeningPort}.",
                sessionId,
                listeningPort);

            var tcpListener = _listeners.GetOrAdd(
                listeningPort,
                port => new TcpListener(IPAddress.Loopback, port));

            if (!tcpListener.Server.IsBound)
            {
                try
                {
                    tcpListener.Start();
                }
                catch (SocketException ex)
                {
                    _logger.LogError(ex, "Error while starting TCP listener.");
                    return Result.Fail<Task>($"Socket error on agent: {ex.SocketErrorCode}").AsTaskResult();
                }
            }

            var listenerTask = Task.Run(async () =>
            {
                try
                {
                    var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                    var ws = new ClientWebSocket();
                    await ws.ConnectAsync(websocketUri, linkedTokenSource.Token);
                    var client = await tcpListener.AcceptTcpClientAsync(linkedTokenSource.Token);

                    await ProxyConnections(sessionId, ws, client, linkedTokenSource.Token);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("TCP listening task cancelled.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while listening for TCP client to proxy.");
                }
            }, cancellationToken);

            return Result.Ok(listenerTask).AsTaskResult();
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("TCP listening task cancelled unexpectedly.");
            return Result.Fail<Task>("Listening task cancelled unexpectedly.").AsTaskResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while proxying stream.");
            return Result.Fail<Task>("Error while creating proxy session.").AsTaskResult();
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
            await _retryer.Retry(
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
                    break;
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