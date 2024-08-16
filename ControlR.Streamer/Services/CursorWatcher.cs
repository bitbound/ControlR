using Bitbound.SimpleMessenger;
using ControlR.Streamer.Messages;
using ControlR.Viewer.Models.Messages;
using Microsoft.Extensions.Hosting;

namespace ControlR.Streamer.Services;


internal class CursorWatcher(
    IMessenger _messenger,
    IWin32Interop _win32Interop,
    IDelayer _delayer,
    ILogger<ClipboardManager> _logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var currentCursor = _win32Interop.GetCurrentCursor();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var nextCursor = _win32Interop.GetCurrentCursor();
                if (currentCursor != nextCursor)
                {
                    currentCursor = nextCursor;
                    await _messenger.Send(new CursorChangedMessage(currentCursor));
                }

                await _delayer.Delay(TimeSpan.FromMilliseconds(500), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while getting mouse cursor.");
            }

            await _delayer.Delay(TimeSpan.FromMilliseconds(500), stoppingToken);
        }
    }
}
