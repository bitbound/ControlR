using Microsoft.Extensions.Hosting;


namespace ControlR.Streamer.Services;

internal class InputDesktopReporter(
    IWin32Interop _win32Interop,
    IDelayer _delayer,
    ILogger<InputDesktopReporter> _logger) : BackgroundService
{


    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Beginning desktop watch.");


        if (_win32Interop.GetInputDesktop(out var initialInputDesktop))
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
                await _delayer.Delay(TimeSpan.FromMilliseconds(100), stoppingToken);

                if (!_win32Interop.GetInputDesktop(out var inputDesktop))
                {
                    _logger.LogError("Failed to get input desktop.");
                    break;
                }

                if (!_win32Interop.GetCurrentThreadDesktop(out var threadDesktop))
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
