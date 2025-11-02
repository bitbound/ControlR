using Bitbound.SimpleMessenger;
using ControlR.DesktopClient.Common.Messages;
using ControlR.Libraries.DevicesCommon.Services.Processes;
using ControlR.Libraries.NativeInterop.Windows;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ControlR.DesktopClient.Windows.Services;

internal class CursorWatcherWindows(
  IMessenger messenger,
  IWin32Interop win32Interop,
  IProcessManager processManager,
  ILogger<CursorWatcherWindows> logger) : BackgroundService
{
  private readonly ILogger<CursorWatcherWindows> _logger = logger;
  private readonly IMessenger _messenger = messenger;
  private readonly IProcessManager _processManager = processManager;
  private readonly IWin32Interop _win32Interop = win32Interop;

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    if (_processManager.GetCurrentProcess().SessionId == 0)
    {
      _logger.LogInformation("Skipping cursor icon watch due to being in session 0.");
      return;
    }

    var currentCursor = _win32Interop.GetCurrentCursor();

    while (!stoppingToken.IsCancellationRequested)
    {
      try
      {
        await Task.Delay(TimeSpan.FromMilliseconds(10), stoppingToken);

        var nextCursor = _win32Interop.GetCurrentCursor();
        if (currentCursor == nextCursor)
        {
          continue;
        }

        currentCursor = nextCursor;
        await _messenger.Send(new CursorChangedMessage(currentCursor));
      }
      catch (OperationCanceledException)
      {
        _logger.LogInformation("Cursor watch aborted.  Application shutting down.");
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error while getting mouse cursor.");
      }
    }
  }
}