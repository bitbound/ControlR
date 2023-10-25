using Bitbound.SimpleMessenger;
using ControlR.Agent.Messages;
using ControlR.Agent.Models;
using ControlR.Devices.Common.Services;
using ControlR.Shared;
using ControlR.Shared.Extensions;
using ControlR.Shared.Helpers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Buffers;
using System.Net.Sockets;
using System.Net.WebSockets;

namespace ControlR.Agent.Services;

internal class LocalProxy(
    IHostApplicationLifetime appLifetime,
    IMessenger messenger,
    IProcessInvoker processInvoker,
    IOptionsMonitor<AppOptions> appOptions,
    ILogger<LocalProxy> logger) : IHostedService
{
    private readonly IHostApplicationLifetime _appLifetime = appLifetime;
    private readonly IOptionsMonitor<AppOptions> _appOptions = appOptions;
    private readonly ILogger<LocalProxy> _logger = logger;
    private readonly IMessenger _messenger = messenger;
    private readonly IProcessInvoker _processInvoker = processInvoker;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _messenger.Register<VncProxyRequestMessage>(this, HandleVncRequestMessage);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private async Task HandleVncRequestMessage(VncProxyRequestMessage message)
    {
        var sessionId = message.Session.SessionId;
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

            ProxyConnections(message, ws, tcpClient).AndForget();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while proxying stream.");
            ws.Dispose();
        }
    }

    private async Task ProxyConnections(
        VncProxyRequestMessage message,
        WebSocket serverConnection,
        TcpClient localConnection)
    {
        using var logScope = _logger.BeginScope("Proxying session ID {VncProxySessionId}.", message.Session.SessionId);
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
            _logger.LogInformation("Proxy session ended.  Cleaning up connections.");
            serverConnection.Dispose();
            localConnection.Dispose();
            await message.Session.CleanupFunc.Invoke();
        }
    }

    private async Task ReadFromLocal(TcpClient localConnection, WebSocket serverConnection)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(ushort.MaxValue);

        try
        {
            while (localConnection.Connected &&
                serverConnection.State == WebSocketState.Open &&
                !_appLifetime.ApplicationStopping.IsCancellationRequested)
            {
                var bytesReceived = await localConnection.Client.ReceiveAsync(buffer, _appLifetime.ApplicationStopping);
                if (bytesReceived == 0)
                {
                    continue;
                }

                await serverConnection.SendAsync(
                    buffer.AsMemory()[0..bytesReceived],
                    WebSocketMessageType.Binary,
                    true,
                    _appLifetime.ApplicationStopping);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while reading from local connection.");
        }
        finally
        {
            _logger.LogInformation("Read from local connection ended.");
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private async Task ReadFromServer(WebSocket serverConnection, TcpClient localConnection)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(ushort.MaxValue);

        try
        {
            while (localConnection.Connected &&
              serverConnection.State == WebSocketState.Open &&
              !_appLifetime.ApplicationStopping.IsCancellationRequested)
            {
                var result = await serverConnection.ReceiveAsync(buffer, _appLifetime.ApplicationStopping);
                if (result.Count == 0)
                {
                    continue;
                }

                await localConnection.Client.SendAsync(
                    buffer.AsMemory()[0..result.Count],
                    _appLifetime.ApplicationStopping);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while reading from server connection.");
        }
        finally
        {
            _logger.LogInformation("Read from server ended.");
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}