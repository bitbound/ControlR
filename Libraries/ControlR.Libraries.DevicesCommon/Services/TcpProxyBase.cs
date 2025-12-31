using ControlR.Libraries.Shared.Helpers;
using ControlR.Libraries.Shared.Services.Buffers;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;

namespace ControlR.Libraries.DevicesCommon.Services;

public abstract class TcpWebsocketProxyBase(
    IMemoryProvider memoryProvider,
    ILogger<TcpWebsocketProxyBase> logger)
{
  protected readonly ILogger<TcpWebsocketProxyBase> _logger = logger;
  protected readonly IMemoryProvider _memoryProvider = memoryProvider;

  protected async Task<Result> ProxyToLocalService(
      Guid sessionId,
      int localServicePort,
      Uri websocketUri,
      CancellationToken cancellationToken)
  {

    var tcpClient = new TcpClient();
    try
    {
      _logger.LogInformation("Starting proxy for session ID {SessionID} to local service port {LocalServicePort}.",
          sessionId,
          localServicePort);

      await tcpClient.ConnectAsync(IPAddress.Loopback, localServicePort, cancellationToken);
      ConnectWebSocketToTcpClient(sessionId, websocketUri, tcpClient, cancellationToken).Forget();

      return Result.Ok();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while proxying stream.");
      Disposer.DisposeAll(tcpClient);
      return Result.Fail("Error while creating proxy session.");
    }
  }

  private async Task ConnectWebSocketToTcpClient(
     Guid sessionId,
     Uri serverWebsocketUri,
     TcpClient localConnection,
     CancellationToken cancellationToken)
  {
    try
    {
      using var serverConnection = new ClientWebSocket();
      await serverConnection.ConnectAsync(serverWebsocketUri, cancellationToken);
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
      Disposer.DisposeAll(localConnection);
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