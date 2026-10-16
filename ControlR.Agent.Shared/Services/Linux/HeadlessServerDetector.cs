using ControlR.Libraries.Shared.Logging;
using ControlR.Libraries.Shared.Services.Processes;

namespace ControlR.Agent.Shared.Services.Linux;

public interface IHeadlessServerDetector
{
    /// <summary>
    /// Determines if the current system is running in a headless environment (without X11 or Wayland display server).
    /// </summary>
    /// <returns>True if the system is headless, false if a display server is available.</returns>
    Task<bool> IsHeadlessServer();
}

internal class HeadlessServerDetector(
    IProcessManager processManager,
    ILogger<HeadlessServerDetector> logger) : IHeadlessServerDetector
{
    // Markers the DISPLAY probe prints to stdout to self-report its outcome. GetProcessOutput
    // ignores the child's exit code, so without a positive "ran and found nothing" statement a
    // probe that could not run would read as a clean negative. The marker literals in
    // HeadlessServerDetectorTests pin this wire contract.
    private const string DisplayFoundMarker = "cr-display-found";
    private const string DisplayNoneMarker = "cr-display-none";
    private const string DisplayProbeErrorMarker = "cr-display-error";

    // grep's exit status decides the marker: 0 = DISPLAY found, 1 = absent from every environ it
    // could read. Status 2 is a partial scan (an environ owned by another user, or a PID that
    // vanished mid-scan; routine on a multi-user host), and the unreadable files are exactly where
    // another user's DISPLAY lives, so no negative can be claimed from it. Every other status means
    // the scan never ran (126/127 = grep could not execute, 128+ = killed by a signal). Both
    // report the error marker. Single quotes protect the grep pattern; the group-level stderr
    // redirect keeps grep's per-file "Permission denied" out of the agent log, because
    // GetProcessOutput leaves the child's stderr inherited.
    private static readonly string _displayProbeArguments =
        $"-c \"{{ grep -qz '^DISPLAY=' /proc/[0-9]*/environ; case $? in 0) echo {DisplayFoundMarker};; 1) echo {DisplayNoneMarker};; *) echo {DisplayProbeErrorMarker};; esac; }} 2>/dev/null\"";

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
                _logger.LogDebugDeduped("X11 display server detected. Not headless.");
                return false;
            }

            // Check for Wayland compositor
            var waylandResult = await _processManager.GetProcessOutput("pgrep", "-f wayland", 3000);
            if (waylandResult.IsSuccess && !string.IsNullOrWhiteSpace(waylandResult.Value))
            {
                _logger.LogDebugDeduped("Wayland compositor detected. Not headless.");
                return false;
            }

            // Check if DISPLAY environment variable is set in any running process. The probe
            // self-reports, so anything other than the found or none marker means it could not
            // run and its output carries no information about the display state.
            var displayResult = await _processManager.GetProcessOutput("bash", _displayProbeArguments, 3000);
            if (!displayResult.IsSuccess)
            {
                _logger.LogWarningDeduped("DISPLAY environment probe could not run. Assuming headless environment.");
                return true;
            }

            var displayProbeMarker = displayResult.Value?.Trim();
            if (displayProbeMarker == DisplayFoundMarker)
            {
                _logger.LogDebugDeduped("DISPLAY environment variable found. Not headless.");
                return false;
            }

            if (displayProbeMarker != DisplayNoneMarker)
            {
                // Missing grep, stripped /proc, or a non-GNU grep rejecting -z land here. Fail
                // closed instead of reading a broken probe as a confirmed negative.
                _logger.LogWarningDeduped("DISPLAY environment probe returned no known marker. Assuming headless environment.");
                return true;
            }

            _logger.LogInformationDeduped("No display server detected. Assuming headless environment.");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error detecting headless environment. Assuming desktop environment is available.");
            return false;
        }
    }
}