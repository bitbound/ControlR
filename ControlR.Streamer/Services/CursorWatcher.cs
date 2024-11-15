using Bitbound.SimpleMessenger;
using ControlR.Streamer.Messages;
using Microsoft.Extensions.Hosting;

namespace ControlR.Streamer.Services;

internal class CursorWatcher(
  IMessenger messenger,
  IWin32Interop win32Interop,
  IDelayer delayer,
  ILogger<ClipboardManager> logger) : BackgroundService
{
  private readonly IMessenger _messenger = messenger;
  private readonly IWin32Interop _win32Interop = win32Interop;
  private readonly IDelayer _delayer = delayer;
  private readonly ILogger<ClipboardManager> _logger = logger;

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
      }
      catch (OperationCanceledException)
      {
        _logger.LogInformation("Cursor watch aborted.  Application shutting down.");
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error while getting mouse cursor.");
      }

      await _delayer.Delay(TimeSpan.FromMilliseconds(50), stoppingToken);
    }
  }
}