using ControlR.Devices.Common.Native.Windows;
using ControlR.Devices.Common.Services;
using ControlR.Shared.Dtos.SidecarDtos;
using ControlR.Streamer.Sidecar.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Runtime.Versioning;

namespace ControlR.Streamer.Sidecar.Services;

[SupportedOSPlatform("windows6.0.6000")]
internal class InputDesktopReporter(
    IHostApplicationLifetime _hostLifetime,
    IOptions<StartupOptions> _startupOptions,
    IProcessManager _processes,
    IStreamerIpcConnection _streamerIpc,
    ILogger<InputDesktopReporter> _logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await WatchDesktop(stoppingToken);
        _logger.LogInformation("Exiting desktop watcher.");
        _hostLifetime.StopApplication();
    }

    private async Task WatchDesktop(CancellationToken cancellationToken)
    {
        var parentId = _startupOptions.Value.ParentProcessId;

        if (parentId == -1)
        {
            _logger.LogError("Parent process ID shouldn't be -1 here.");
            return;
        }

        var parentProcess = _processes.GetProcessById(parentId);

        if (parentProcess is null)
        {
            _logger.LogError("Parent process ID {ProcessId} doesn't exist.", parentId);
            return;
        }

        _logger.LogInformation("Beginning desktop watch for streamer process: {ParentProcessId}", parentId);


        if (Win32.GetInputDesktop(out var initialInputDesktop))
        {
            _logger.LogInformation("Initial desktop: {DesktopName}", initialInputDesktop);
        }
        else
        {
            _logger.LogWarning("Failed to get initial desktop.");
        }

        if (Win32.SwitchToInputDesktop())
        {
            _logger.LogInformation("Switched to initial input desktop.");
        }
        else
        {
            _logger.LogWarning("Failed to switch to initial input desktop.");
        }

        while (!_hostLifetime.ApplicationStopping.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(50, _hostLifetime.ApplicationStopping);

                if (parentProcess.HasExited)
                {
                    _logger.LogInformation("Parent ID {ParentProcessId} no longer exists.  Exiting watcher process.", parentId);
                    return;
                }

                if (!Win32.GetInputDesktop(out var inputDesktop))
                {
                    _logger.LogError("Failed to get input desktop.");
                    break;
                }

                if (!Win32.GetCurrentThreadDesktop(out var threadDesktop))
                {
                    _logger.LogError("Failed to get thread desktop.");
                    break;
                }

                if (!string.IsNullOrWhiteSpace(inputDesktop) &&
                    !string.IsNullOrWhiteSpace(threadDesktop) &&
                    !string.Equals(inputDesktop, threadDesktop, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation(
                        "Desktop has changed from {LastDesktop} to {CurrentDesktop}.  Sending to agent.",
                        threadDesktop,
                        inputDesktop);

                    await _streamerIpc.Send(new DesktopChangedDto(inputDesktop));

                    if (!Win32.SwitchToInputDesktop())
                    {
                        _logger.LogWarning("Failed to switch to input desktop.");
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while reporting input desktop.");
                break;
            }
        }
    }
}
