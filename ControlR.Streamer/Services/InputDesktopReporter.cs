using Microsoft.Extensions.Hosting;

namespace ControlR.Streamer.Services;

internal class InputDesktopReporter(
  IWin32Interop win32Interop,
  IDelayer delayer,
  ILogger<InputDesktopReporter> logger) : BackgroundService
{
  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    logger.LogInformation("Beginning desktop watch.");


    if (win32Interop.GetInputDesktop(out var initialInputDesktop))
    {
      logger.LogInformation("Initial desktop: {DesktopName}", initialInputDesktop);
    }
    else
    {
      logger.LogWarning("Failed to get initial desktop.");
    }

    if (win32Interop.SwitchToInputDesktop())
    {
      logger.LogInformation("Switched to initial input desktop.");
    }
    else
    {
      logger.LogWarning("Failed to switch to initial input desktop.");
    }

    while (!stoppingToken.IsCancellationRequested)
    {
      try
      {
        await delayer.Delay(TimeSpan.FromMilliseconds(100), stoppingToken);

        if (!win32Interop.GetInputDesktop(out var inputDesktop))
        {
          logger.LogError("Failed to get input desktop.");
          break;
        }

        if (!win32Interop.GetCurrentThreadDesktop(out var threadDesktop))
        {
          logger.LogError("Failed to get thread desktop.");
          break;
        }

        if (!string.IsNullOrWhiteSpace(inputDesktop) &&
            !string.IsNullOrWhiteSpace(threadDesktop) &&
            !string.Equals(inputDesktop, threadDesktop, StringComparison.OrdinalIgnoreCase))
        {
          logger.LogInformation(
            "Desktop has changed from {LastDesktop} to {CurrentDesktop}.",
            threadDesktop,
            inputDesktop);


          if (!win32Interop.SwitchToInputDesktop())
          {
            logger.LogWarning("Failed to switch to input desktop.");
            break;
          }
        }
      }
      catch (OperationCanceledException)
      {
        logger.LogInformation("Desktop watch cancelled.  Application shutting down.");
      }
      catch (Exception ex)
      {
        logger.LogError(ex, "Error while reporting input desktop.");
        break;
      }
    }
  }
}