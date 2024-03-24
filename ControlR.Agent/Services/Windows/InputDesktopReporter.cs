using ControlR.Agent.Models.IpcDtos;
using ControlR.Devices.Common.Native.Windows;
using ControlR.Devices.Common.Services;
using ControlR.Shared;
using SimpleIpc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Runtime.Versioning;

namespace ControlR.Agent.Services.Windows;

internal interface IInputDesktopReporter
{
    Task Start(string agentPipeName, int parentProcessId);
}

[SupportedOSPlatform("windows6.0.6000")]
internal class InputDesktopReporter(
    IHostApplicationLifetime hostLifetime,
    IProcessManager processes,
    IIpcConnectionFactory ipcFactory,
    ILogger<InputDesktopReporter> logger) : IInputDesktopReporter
{
    private readonly IHostApplicationLifetime _hostLifetime = hostLifetime;
    private readonly IProcessManager _processes = processes;
    private readonly IIpcConnectionFactory _ipcFactory = ipcFactory;
    private readonly ILogger<InputDesktopReporter> _logger = logger;
    private string _agentPipeName = string.Empty;
    private int _parentId;

    public Task Start(string agentPipeName, int parentProcessId)
    {
        _agentPipeName = agentPipeName;
        _parentId = parentProcessId;
        _ = Task.Run(WatchDesktop);

        return Task.CompletedTask;
    }

    private async Task WatchDesktop()
    {
        if (string.IsNullOrWhiteSpace(_agentPipeName))
        {
            _logger.LogError("Agent pipe name cannot be null.");
            _hostLifetime.StopApplication();
            return;
        }

        if (_parentId == -1)
        {
            _logger.LogError("Parent process ID shouldn't be -1 here.");
            _hostLifetime.StopApplication();
            return;
        }

        var parentProcess = _processes.GetProcessById(_parentId);

        _logger.LogInformation("Beginning desktop watch for pipe: {AgentPipeName}", _agentPipeName);

     
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

        _logger.LogInformation("Creating IPC client for pipe name: {AgentPipeName}", _agentPipeName);
        var client = await _ipcFactory.CreateClient(".", _agentPipeName);
        var connected = await client.Connect(10_000);

        if (!connected)
        {
            _logger.LogError("Failed to connect to pipe server host to send desktop change updates.");
            return;
        }

        client.On<DesktopRequestDto, DesktopChangeDto>((_) =>
        {
            if (Win32.GetInputDesktop(out var desktop))
            {
                return new DesktopChangeDto(desktop);
            }
            return new DesktopChangeDto(string.Empty);
        });

        client.BeginRead(_hostLifetime.ApplicationStopping);

        _logger.LogInformation("Connected to pipe server.");

        while (!_hostLifetime.ApplicationStopping.IsCancellationRequested && client.IsConnected)
        {
            try
            {
                await Task.Delay(50, _hostLifetime.ApplicationStopping);

                if (parentProcess.HasExited)
                {
                    _logger.LogInformation("Parent ID {ParentProcessId} no longer exists.  Exiting watcher process.", _parentId);
                    return;
                }

                if (!AnyStreamerProcessesExist())
                {
                    _logger.LogInformation("No more streamers exist in current Windows session.  Exiting.");
                    break;
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
                    await client.Send(new DesktopChangeDto(inputDesktop));
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

        _logger.LogInformation("Exiting desktop watcher for pipe name: {AgentPipeName}", _agentPipeName);
        _hostLifetime.StopApplication();
    }

    private bool AnyStreamerProcessesExist()
    {
        var currentPrcess = _processes.GetCurrentProcess();

        // Wait at least 30 seconds for streamer to start.
        if (DateTime.Now - currentPrcess.StartTime < TimeSpan.FromSeconds(30))
        {
            return true;
        }

        var streamers = _processes
            .GetProcessesByName(Path.GetFileNameWithoutExtension(AppConstants.RemoteControlFileName))
            .Where(x => x.SessionId == currentPrcess.SessionId);
        return streamers.Any();
    }
}
