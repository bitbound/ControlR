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

    // grep's exit status decides the marker: 0 = DISPLAY found, 1 = genuinely absent from every
    // readable environ. Status 2 is ambiguous: grep reports it both for per-file read errors (an
    // environ owned by another user, or a PID that vanished mid-scan; routine on a busy host) and
    // for a fatal misuse such as a non-GNU grep rejecting -z. Disambiguate: when no environ is
    // readable the probe cannot answer; when one is readable, re-test -z support against
    // /dev/null and report the clean negative only if the option is accepted. Every other status
    // (126/127 = grep could not execute, 128+ = killed by a signal) means the scan never
    // happened, so report the error marker without consulting readability. The readability loop
    // uses only bash builtins; the capability re-test is the single extra process and runs only
    // on the status-2 path. Single quotes protect the grep pattern; $f/$r stay unquoted because
    // the whole script is wrapped in double quotes for the ProcessStartInfo argument, and an
    // inner double quote would terminate that argument early. The group-level stderr redirect
    // keeps error text ("command not found", grep's per-file "Permission denied", usage output
    // from a rejected option) out of the agent log; GetProcessOutput leaves the child's stderr
    // inherited.
    private static readonly string DisplayProbeArguments =
        $"-c \"{{ grep -qz '^DISPLAY=' /proc/[0-9]*/environ; case $? in 0) echo {DisplayFoundMarker};; 1) echo {DisplayNoneMarker};; 2) r=0; for f in /proc/[0-9]*/environ; do if [ -r $f ]; then r=1; break; fi; done; if [ $r = 0 ]; then echo {DisplayProbeErrorMarker}; else grep -zq x /dev/null; case $? in 0|1) echo {DisplayNoneMarker};; *) echo {DisplayProbeErrorMarker};; esac; fi;; *) echo {DisplayProbeErrorMarker};; esac; }} 2>/dev/null\"";

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
                _logger.LogDebugDeduped("X11 display server detected - not headless.");
                return false;
            }

            // Check for Wayland compositor
            var waylandResult = await _processManager.GetProcessOutput("pgrep", "-f wayland", 3000);
            if (waylandResult.IsSuccess && !string.IsNullOrWhiteSpace(waylandResult.Value))
            {
                _logger.LogDebugDeduped("Wayland compositor detected - not headless.");
                return false;
            }

            // Check if DISPLAY environment variable is set in any running process. The probe
            // self-reports, so anything other than the found or none marker means it could not
            // run and its output carries no information about the display state.
            var displayResult = await _processManager.GetProcessOutput("bash", DisplayProbeArguments, 3000);
            if (!displayResult.IsSuccess)
            {
                _logger.LogWarningDeduped("DISPLAY environment probe could not run. Assuming headless environment.");
                return true;
            }

            var displayProbeMarker = displayResult.Value?.Trim();
            if (displayProbeMarker == DisplayFoundMarker)
            {
                _logger.LogDebugDeduped("DISPLAY environment variable found - not headless.");
                return false;
            }

            if (displayProbeMarker != DisplayNoneMarker)
            {
                // Missing grep, stripped /proc, or a non-GNU grep rejecting -z land here. Fail
                // closed instead of reading a broken probe as a confirmed negative.
                _logger.LogWarningDeduped("DISPLAY environment probe returned no known marker. Assuming headless environment.");
                return true;
            }

            _logger.LogInformationDeduped("No display server detected - assuming headless environment.");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error detecting headless environment. Assuming desktop environment is available.");
            return false;
        }
    }
}