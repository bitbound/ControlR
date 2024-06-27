using Bitbound.SimpleMessenger;
using ControlR.Libraries.Shared.Hubs;
using ControlR.Libraries.Shared.Interfaces.HubClients;
using ControlR.Streamer.Messages;
using ControlR.Viewer.Models.Messages;
using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Net.WebSockets;

namespace ControlR.Streamer.Services;

public interface IStreamerHubConnection : IHubConnectionBase, IHostedService
{
}
internal class StreamerHubConnection(
    IServiceProvider _services,
    IMessenger _messenger,
    IDelayer _delayer,
    IHostApplicationLifetime _appLifetime,
    IToaster _toaster,
    IDisplayManager _displayManager,
    IOptions<StartupOptions> _startupOptions,
    ILogger<StreamerHubConnection> _logger)
    : HubConnectionBase(_services, _messenger, _delayer, _logger), IStreamerHubConnection, IStreamerHubClient
{

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Connect(
                () => $"{_startupOptions.Value.ServerOrigin}hubs/streamer",
                ConfigureConnection,
                ConfigureHttpOptions,
                false,
                _appLifetime.ApplicationStopping);

            await SendStreamerInitData();

            _messenger.Register<LocalClipboardChangedMessage>(this, HandleLocalClipboardChanged);
            _messenger.Register<DisplaySettingsChangedMessage>(this, HandleDisplaySettingsChanged);

            if (_startupOptions.Value.NotifyUser)
            {
                var message = _startupOptions.Value.ViewerName is { Length: > 0 } viewerName ?
                    $"{viewerName} has joined your session" :
                    "Remote control session has started";

                await _toaster.ShowToast("ControlR", message, BalloonTipIcon.Info);
            }

            StreamScreenToViewer().Forget();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error while initializing streaming session. " +
                "Streaming cannot start.  Shutting down.");
            _appLifetime.StopApplication();
        }
    }

    private async Task HandleDisplaySettingsChanged(object subscriber, DisplaySettingsChangedMessage message)
    {
        _displayManager.ResetDisplays();
        await SendStreamerInitData();
    }

    private async Task StreamScreenToViewer()
    {
        using var ws = new ClientWebSocket();
        var origin = _startupOptions.Value.ServerOrigin
            .ToWebsocketUri()
            .ToString()
            .TrimEnd('/');

        var wsEndpoint = $"{origin}/streamer-ws-endpoint/{_startupOptions.Value.SessionId}";
        await ws.ConnectAsync(new Uri(wsEndpoint), _appLifetime.ApplicationStopping);

        while (ws.State == WebSocketState.Open && !_appLifetime.ApplicationStopping.IsCancellationRequested)
        {
            try
            {
                await foreach (var region in _displayManager.GetChangedRegions())
                {
                    await ws.SendAsync(
                        region,
                        WebSocketMessageType.Binary,
                        true,
                        _appLifetime.ApplicationStopping);
                }
                
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while sending screen frame.");
                break;
            }
        }

        _logger.LogInformation("Streaming session ended.  Shutting down.");
        _appLifetime.StopApplication();
    }

    private async Task HandleLocalClipboardChanged(object subscriber, LocalClipboardChangedMessage message)
    {
        try
        {
            await Connection.InvokeAsync(
                nameof(IStreamerHub.SendClipboardChangeToViewer), 
                _startupOptions.Value.ViewerConnectionId,
                new ClipboardChangeDto(message.Text, _startupOptions.Value.SessionId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while handling local clipboard change.");
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await StopConnection(cancellationToken);
    }

    private void ConfigureConnection(HubConnection connection)
    {
        connection.Closed += Connection_Closed;
    }

    private Task Connection_Closed(Exception? exception)
    { 
        _appLifetime.StopApplication();
        return Task.CompletedTask;
    }

    private void ConfigureHttpOptions(HttpConnectionOptions options)
    {
    }

    private async Task SendStreamerInitData()
    {
        try
        {
            var displays = _displayManager.GetDisplays().ToArray();
            var dto = new StreamerInitDataDto(
                _startupOptions.Value.SessionId,
                Connection.ConnectionId ?? "",
                displays);

            await Connection.InvokeAsync(
                nameof(IStreamerHub.SendStreamerInitDataToViewer),
                _startupOptions.Value.ViewerConnectionId,
                dto);


        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error while sending streamer init data. " +
                "Streaming cannot start.  Shutting down.");
            _appLifetime.StopApplication();
        }
    }
}
