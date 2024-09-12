using Bitbound.SimpleMessenger;
using ControlR.Streamer.Messages;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Net.WebSockets;
using ControlR.Libraries.Shared.Services.Buffers;

namespace ControlR.Streamer.Services;

public interface IStreamerStreamingClient : IHostedService
{
}

internal sealed class StreamerStreamingClient(
    IMessenger messenger,
    IHostApplicationLifetime _appLifetime,
    IToaster _toaster,
    IDisplayManager _displayManager,
    IKeyProvider _keyProvider,
    IMemoryProvider _memoryProvider,
    IOptions<StartupOptions> _startupOptions,
    ILogger<StreamerStreamingClient> _logger)
    : StreamingClient(_keyProvider, messenger, _memoryProvider, _logger), IStreamerStreamingClient
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Connect(_startupOptions.Value.WebSocketUri, _appLifetime.ApplicationStopping);
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

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await DisposeAsync();
    }

    private async Task HandleCursorChangedMessage(object subscriber, CursorChangedMessage message)
    {
        try
        {
            var dto = new CursorChangedDto(message.Cursor, _startupOptions.Value.SessionId);
            var wrapper = DtoWrapper.Create(dto, DtoType.CursorChanged);
            await Send(wrapper, _appLifetime.ApplicationStopping);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while handling cursor change.");
        }
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
            var wrapper = DtoWrapper.Create(dto, DtoType.ClipboardChanged);
            await Send(wrapper, _appLifetime.ApplicationStopping);
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

            var wrapper = DtoWrapper.Create(dto, DtoType.DisplayData);
            await Send(wrapper, _appLifetime.ApplicationStopping);
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

        while (State == WebSocketState.Open && !_appLifetime.ApplicationStopping.IsCancellationRequested)
        {
            try
            {
                await foreach (var region in _displayManager.GetChangedRegions())
                {
                    var wrapper = DtoWrapper.Create(region, DtoType.ScreenRegion);
                    await Send(wrapper, _appLifetime.ApplicationStopping);
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
