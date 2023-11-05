using ControlR.Agent.Models;
using ControlR.Shared;
using ControlR.Shared.Extensions;
using ControlR.Shared.Helpers;
using ControlR.Shared.Services.Buffers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Sockets;
using System.Net.WebSockets;

namespace ControlR.Agent.Services;

internal interface ILocalProxy
{
    Task HandleVncSession(VncSession session);
}

internal class LocalProxy(
    IHostApplicationLifetime _appLifetime,
    IOptionsMonitor<AppOptions> _appOptions,
    IMemoryProvider _memoryProvider,
    ILogger<LocalProxy> _logger) : ILocalProxy
{
    public async Task HandleVncSession(VncSession session)
    {
        var sessionId = session.SessionId;
        var ws = new ClientWebSocket();

        try
        {
            var vncPort = _appOptions.CurrentValue.VncPort;

            if (vncPort is null)
            {
                _logger.LogError("VNC port is empty in appsettings.  Aborting VNC proxy.");
                return;
            }

            _logger.LogInformation("Starting proxy for session ID {SessionID} to port {VncPort}.",
                sessionId,
                vncPort);

            var tcpClient = new TcpClient();
            await TryHelper.Retry(
                async () =>
                {
                    await tcpClient.ConnectAsync("127.0.0.1", vncPort.Value);
                },
                tryCount: 3,
                retryDelay: TimeSpan.FromSeconds(3));

            var websocketEndpoint = new Uri($"{AppConstants.ServerUri.Replace("http", "ws")}/agentvnc-proxy/{sessionId}");
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
            _logger.LogInformation("Proxy session ended.  Cleaning up connections.  Session ID: {SessionID}", session.SessionId);
            DisposeHelper.DisposeAll(serverConnection, localConnection);
            await session.DisposeAsync();
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
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.OperationAborted)
        {
            _logger.LogInformation("Websocket connection aborted.");
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
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.OperationAborted)
        {
            _logger.LogInformation("Websocket connection aborted.");
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