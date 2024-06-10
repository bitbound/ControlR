using ControlR.Libraries.Shared.Dtos.SidecarDtos;
using ControlR.Streamer.Sidecar.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Runtime.Versioning;
using ControlR.Devices.Native.Services;
using ControlR.Libraries.DevicesCommon.Services;
using ControlR.Libraries.Shared.Extensions;

namespace ControlR.Streamer.Sidecar.Services;

[SupportedOSPlatform("windows6.0.6000")]
internal class InputDesktopReporter(
    IHostApplicationLifetime _hostLifetime,
    IWin32Interop _win32Interop,
    IOptions<StartupOptions> _startupOptions,
    IProcessManager _processes,
    IStreamerIpcConnection _streamerIpc,
    ILogger<InputDesktopReporter> _logger) : IHostedService
{
    private Thread? _watcherThread;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _watcherThread = new Thread(() =>
        {
            WatchDesktop(_hostLifetime.ApplicationStopping);
        });
        _watcherThread.SetApartmentState(ApartmentState.STA);
        _watcherThread.Start();

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private void WatchDesktop(CancellationToken cancellationToken)
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

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                Thread.Sleep(100);

                if (parentProcess.HasExited)
                {
                    _logger.LogInformation("Parent ID {ParentProcessId} no longer exists.  Exiting watcher process.", parentId);
                    break;
                }

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
                        "Desktop has changed from {LastDesktop} to {CurrentDesktop}.  Sending to streamer.",
                        threadDesktop,
                        inputDesktop);

                    _streamerIpc.Send(new DesktopChangedDto(inputDesktop)).Forget();

                    if (!_win32Interop.SwitchToInputDesktop())
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
