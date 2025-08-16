using ControlR.Libraries.NativeInterop.Windows;
using ControlR.Libraries.Shared.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ControlR.DesktopClient.Windows.Services;

internal class InputDesktopReporter(
  TimeProvider timeProvider,
  IWin32Interop win32Interop,
  IDelayer delayer,
  ILogger<InputDesktopReporter> logger) : BackgroundService
{
  private readonly TimeProvider _timeProvider = timeProvider;
  private readonly IWin32Interop _win32Interop = win32Interop;
  private readonly IDelayer _delayer = delayer;
  private readonly ILogger<InputDesktopReporter> _logger = logger;

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    _logger.LogInformation("Beginning desktop watch.");


    if (_win32Interop.GetInputDesktopName(out var initialInputDesktop))
    {
      _logger.LogInformation("Initial desktop: {DesktopName}", initialInputDesktop);
    }
    else
    {
      _logger.LogWarning("Failed to get initial desktop.");
    }

    if (_win32Interop.SwitchToInputDesktop())
    {
      _logger.LogInformation("Switched to initial input desktop.");
    }
    else
    {
      _logger.LogWarning("Failed to switch to initial input desktop.");
    }

    while (!stoppingToken.IsCancellationRequested)
    {
      try
      {
        await Task.Delay(TimeSpan.FromMilliseconds(100), _timeProvider, stoppingToken);

        if (!_win32Interop.GetInputDesktopName(out var inputDesktop))
        {
          _logger.LogError("Failed to get input desktop.");
          break;
        }

        if (!_win32Interop.GetCurrentThreadDesktopName(out var threadDesktop))
        {
          _logger.LogError("Failed to get thread desktop.");
          break;
        }

        if (!string.IsNullOrWhiteSpace(inputDesktop) &&
            !string.IsNullOrWhiteSpace(threadDesktop) &&
            !string.Equals(inputDesktop, threadDesktop, StringComparison.OrdinalIgnoreCase))
        {
          _logger.LogInformation(
            "Desktop has changed from {LastDesktop} to {CurrentDesktop}.",
            threadDesktop,
            inputDesktop);


          if (!_win32Interop.SwitchToInputDesktop())
          {
            _logger.LogWarning("Failed to switch to input desktop.");
            break;
          }
        }
      }
      catch (OperationCanceledException)
      {
        _logger.LogInformation("Desktop watch cancelled.  Application shutting down.");
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error while reporting input desktop.");
        break;
      }
    }
  }
}