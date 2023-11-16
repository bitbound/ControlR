using ControlR.Agent.Interfaces;
using ControlR.Agent.Models;
using ControlR.Shared.Extensions;
using ControlR.Shared.Helpers;
using ControlR.Shared.Services.Buffers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net.Sockets;
using System.Net.WebSockets;

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
    ILogger<LocalProxy> _logger) : ILocalProxy
{
    private volatile int _sessionCount;

    public async Task HandleVncSession(VncSession session)
    {
        var sessionId = session.SessionId;
        var ws = new ClientWebSocket();

        try
        {
            _logger.LogInformation("Starting proxy for session ID {SessionID} to port {VncPort}.",
                sessionId,
                _settings.VncPort);

            var tcpClient = new TcpClient();
            await TryHelper.Retry(
                async () =>
                {
                    await tcpClient.ConnectAsync("127.0.0.1", _settings.VncPort);
                },
                tryCount: 3,
                retryDelay: TimeSpan.FromSeconds(3));

            var serverUri = _settings.ServerUri.ToString().TrimEnd('/');
            var websocketEndpoint = new Uri($"{serverUri.Replace("http", "ws")}/agentvnc-proxy/{sessionId}");
            await ws.ConnectAsync(websocketEndpoint, _appLifetime.ApplicationStopping);

            ProxyConnections(session, ws, tcpClient).AndForget();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while proxying stream.");
            ws.Dispose();
        }
    }

    private async Task ProxyConnections(
        VncSession session,
        WebSocket serverConnection,
        TcpClient localConnection)
    {
        try
        {
            Interlocked.Increment(ref _sessionCount);
            var localReadTask = ReadFromLocal(localConnection, serverConnection);
            var serverReadTask = ReadFromServer(serverConnection, localConnection);

            await Task.WhenAny(localReadTask, serverReadTask);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while proxying connections.");
        }
        finally
        {
            Interlocked.Decrement(ref _sessionCount);
            _logger.LogInformation("Proxy session ended.  Cleaning up connections.  Session ID: {SessionID}", session.SessionId);
            DisposeHelper.DisposeAll(serverConnection, localConnection);
            if (_sessionCount == 0)
            {
                await _vncSessionLauncher.CleanupSessions();
            }
        }
    }

    private async Task ReadFromLocal(TcpClient localConnection, WebSocket serverConnection)
    {
        using var buffer = _memoryProvider.CreateEphemeralBuffer<byte>(ushort.MaxValue);

        try
        {
            while (localConnection.Connected &&
                serverConnection.State == WebSocketState.Open &&
                !_appLifetime.ApplicationStopping.IsCancellationRequested)
            {
                var bytesReceived = await localConnection.Client.ReceiveAsync(buffer.Value, _appLifetime.ApplicationStopping);
                if (bytesReceived == 0)
                {
                    continue;
                }

                await serverConnection.SendAsync(
                    buffer.Value.AsMemory()[0..bytesReceived],
                    WebSocketMessageType.Binary,
                    true,
                    _appLifetime.ApplicationStopping);
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

    private async Task ReadFromServer(WebSocket serverConnection, TcpClient localConnection)
    {
        using var buffer = _memoryProvider.CreateEphemeralBuffer<byte>(ushort.MaxValue);

        try
        {
            while (localConnection.Connected &&
              serverConnection.State == WebSocketState.Open &&
              !_appLifetime.ApplicationStopping.IsCancellationRequested)
            {
                var result = await serverConnection.ReceiveAsync(buffer.Value, _appLifetime.ApplicationStopping);

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
                    _appLifetime.ApplicationStopping);
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