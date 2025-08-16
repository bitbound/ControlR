using Bitbound.SimpleMessenger;
using ControlR.DesktopClient.Common.Messages;
using ControlR.Libraries.NativeInterop.Windows;
using ControlR.Libraries.Shared.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ControlR.DesktopClient.Windows.Services;

internal partial class CursorWatcher(
  TimeProvider timeProvider,
  ISystemEnvironment systemEnvironment,
  IMessenger messenger,
  IWin32Interop win32Interop,
  ILogger<ClipboardManagerWindows> logger) : BackgroundService
{
  private readonly TimeProvider _timeProvider = timeProvider;
  private readonly IMessenger _messenger = messenger;
  private readonly ISystemEnvironment _systemEnvironment = systemEnvironment;
  private readonly IWin32Interop _win32Interop = win32Interop;
  private readonly ILogger<ClipboardManagerWindows> _logger = logger;

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    if (_systemEnvironment.IsSessionZero())
    {
      _logger.LogInformation("Skipping cursor icon watch due to being in session 0.");
      return;
    }

    var currentCursor = _win32Interop.GetCurrentCursor();

    while (!stoppingToken.IsCancellationRequested)
    {
      try
      {
        await Task.Delay(TimeSpan.FromMilliseconds(50), _timeProvider, stoppingToken);

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
    }
  }
}