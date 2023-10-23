using Bitbound.SimpleMessenger;
using ControlR.Agent.Messages;
using ControlR.Agent.Models;
using ControlR.Devices.Common.Services;
using ControlR.Shared.Helpers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Buffers;
using System.Net.Sockets;

namespace ControlR.Agent.Services;

internal class LocalProxy(
    IAgentHubConnection agentHub,
    IHostApplicationLifetime appLifetime,
    IMessenger messenger,
    IProcessInvoker processInvoker,
    IOptionsMonitor<AppOptions> appOptions,
    ILogger<LocalProxy> logger) : IHostedService
{
    private readonly IAgentHubConnection _agentHub = agentHub;
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

            var outgoingTask = ReadFromClient(tcpClient, sessionId);
            var incomingTask = ReadFromHub(tcpClient, sessionId);

            await Task.WhenAny(outgoingTask, incomingTask);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while proxying stream.");
        }
        finally
        {
            await message.Session.CleanupFunc.Invoke();
        }
    }

    private async Task ReadFromClient(TcpClient tcpClient, Guid sessionId)
    {
        using var endSignal = new SemaphoreSlim(0, 1);
        async IAsyncEnumerable<byte[]> ReadFromTcpClient()
        {
            while (tcpClient.Connected &&
                   !_appLifetime.ApplicationStopping.IsCancellationRequested)
            {
                var buffer = ArrayPool<byte>.Shared.Rent(ushort.MaxValue);

                var bytesReceived = await tcpClient.Client.ReceiveAsync(buffer, _appLifetime.ApplicationStopping);
                if (bytesReceived == 0)
                {
                    continue;
                }
                yield return buffer[0..bytesReceived];

                ArrayPool<byte>.Shared.Return(buffer);
            }

            endSignal.Release();
        }

        await _agentHub.SendVncStream(sessionId, ReadFromTcpClient());
        await endSignal.WaitAsync(_appLifetime.ApplicationStopping);
    }

    private async Task ReadFromHub(TcpClient tcpClient, Guid sessionId)
    {
        var incomingStream = _agentHub.GetVncStream(sessionId);
        await foreach (var chunk in incomingStream)
        {
            if (!tcpClient.Connected ||
                _appLifetime.ApplicationStopping.IsCancellationRequested)
            {
                break;
            }
            await tcpClient.Client.SendAsync(chunk);
        }
    }
}