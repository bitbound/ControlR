using ControlR.Agent.Common.Interfaces;
using ControlR.Libraries.DevicesCommon.Services.Processes;

namespace ControlR.Agent.Common.Services.Linux;

internal class HeadlessServerDetector(
    IProcessManager processManager,
    ILogger<HeadlessServerDetector> logger) : IHeadlessServerDetector
{
    private readonly ILogger<HeadlessServerDetector> _logger = logger;
    private readonly IProcessManager _processManager = processManager;

    public async Task<bool> IsHeadlessServer()
    {
        try
        {
            // Check for X11 display server
            var x11Result = await _processManager.GetProcessOutput("pgrep", "-x Xorg", 3000);
            if (x11Result.IsSuccess && !string.IsNullOrWhiteSpace(x11Result.Value))
            {
                _logger.LogIfChanged(LogLevel.Debug, "X11 display server detected - not headless.");
                return false;
            }

            // Check for Wayland compositor
            var waylandResult = await _processManager.GetProcessOutput("pgrep", "-f wayland", 3000);
            if (waylandResult.IsSuccess && !string.IsNullOrWhiteSpace(waylandResult.Value))
            {
                _logger.LogIfChanged(LogLevel.Debug, "Wayland compositor detected - not headless.");
                return false;
            }

            // Check if DISPLAY environment variable is set in any running process
            var displayResult = await _processManager.GetProcessOutput("bash", "-c \"ps -eo env | grep -q DISPLAY && echo 'found'\"", 3000);
            if (displayResult.IsSuccess && !string.IsNullOrWhiteSpace(displayResult.Value) && displayResult.Value.Contains("found"))
            {
                _logger.LogIfChanged(LogLevel.Debug, "DISPLAY environment variable found - not headless.");
                return false;
            }

            // Check if we're running on Ubuntu Server (which typically doesn't have GUI packages)
            var ubuntuServerResult = await _processManager.GetProcessOutput("dpkg", "-l ubuntu-server", 3000);
            if (ubuntuServerResult.IsSuccess)
            {
                _logger.LogIfChanged(LogLevel.Information, "Ubuntu Server package detected - assuming headless environment.");
                return true;
            }

            // If we can't find any display server or GUI indicators, assume headless
            _logger.LogIfChanged(LogLevel.Information, "No display server detected - assuming headless environment.");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error detecting headless environment. Assuming desktop environment is available.");
            return false;
        }
    }
}