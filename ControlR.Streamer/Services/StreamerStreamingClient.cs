using Bitbound.SimpleMessenger;
using ControlR.Libraries.Shared.Hubs;
using ControlR.Libraries.Shared.Interfaces.HubClients;
using Microsoft.Extensions.DependencyInjection;
using ControlR.Streamer.Messages;
using ControlR.Viewer.Models.Messages;
using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Net.WebSockets;

namespace ControlR.Streamer.Services;

public interface IStreamerStreamingClient : IHostedService
{
}
internal class StreamerStreamingClient(
    IServiceProvider _services,
    IMessenger _messenger,
    IHostApplicationLifetime _appLifetime,
    IToaster _toaster,
    IDisplayManager _displayManager,
    IOptions<StartupOptions> _startupOptions,
    ILogger<StreamerStreamingClient> _logger)
    : IStreamerStreamingClient
{
    private IStreamingClient? _client;
    private IStreamingClient Client => _client ?? throw new InvalidOperationException("Streaming client is not initialized.");

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _client = _services.GetRequiredService<IStreamingClient>();

            await _client.Connect(_startupOptions.Value.WebSocketUri, _appLifetime.ApplicationStopping);

            _messenger.Register<LocalClipboardChangedMessage>(this, HandleLocalClipboardChanged);
            _messenger.Register<DisplaySettingsChangedMessage>(this, HandleDisplaySettingsChanged);
            _messenger.Register<CursorChangedMessage>(this, HandleCursorChangedMessage);

            await SendDisplayData();

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

    private async Task HandleCursorChangedMessage(object subscriber, CursorChangedMessage message)
    {
        try
        {
            var dto = new CursorChangedDto(message.Cursor, _startupOptions.Value.SessionId);
            var wrapper = UnsignedPayloadDto.Create(dto, DtoType.CursorChanged);
            await Client.Send(wrapper, _appLifetime.ApplicationStopping);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while handling cursor change.");
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await Client.DisposeAsync();
    }

    private async Task HandleDisplaySettingsChanged(object subscriber, DisplaySettingsChangedMessage message)
    {
        _displayManager.ResetDisplays();
        await SendDisplayData();
    }

    private async Task HandleLocalClipboardChanged(object subscriber, LocalClipboardChangedMessage message)
    {
        try
        {
            var dto = new ClipboardChangeDto(message.Text, _startupOptions.Value.SessionId);
            var wrapper = UnsignedPayloadDto.Create(dto, DtoType.ClipboardChanged);
            await Client.Send(wrapper, _appLifetime.ApplicationStopping);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while handling local clipboard change.");
        }
    }

    private async Task SendDisplayData()
    {
        try
        {
            var displays = _displayManager.GetDisplays().ToArray();
            var dto = new DisplayDataDto(
                _startupOptions.Value.SessionId,
                displays);

            var wrapper = UnsignedPayloadDto.Create(dto, DtoType.DisplayData);
            await Client.Send(wrapper, _appLifetime.ApplicationStopping);
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

    private async Task StreamScreenToViewer()
    {
        await _displayManager.StartCapturingChanges();

        while (Client.State == WebSocketState.Open && !_appLifetime.ApplicationStopping.IsCancellationRequested)
        {
            try
            {
                await foreach (var region in _displayManager.GetChangedRegions())
                {
                    var wrapper = UnsignedPayloadDto.Create(region, DtoType.ScreenRegion);
                    await Client.Send(wrapper, _appLifetime.ApplicationStopping);
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
}
