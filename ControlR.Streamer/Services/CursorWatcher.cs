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
  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    var currentCursor = win32Interop.GetCurrentCursor();

    while (!stoppingToken.IsCancellationRequested)
    {
      try
      {
        var nextCursor = win32Interop.GetCurrentCursor();
        if (currentCursor != nextCursor)
        {
          currentCursor = nextCursor;
          await messenger.Send(new CursorChangedMessage(currentCursor));
        }
      }
      catch (Exception ex)
      {
        logger.LogError(ex, "Error while getting mouse cursor.");
      }

      await delayer.Delay(TimeSpan.FromMilliseconds(50), stoppingToken);
    }
  }
}