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
    // readable environ. grep also returns 2 for reasons that are not the probe failing outright:
    // an individual /proc/[pid]/environ is unreadable (a peer owned by another user, or a PID that
    // vanished before grep reached it). On a busy host that makes the raw status-2 branch the norm,
    // so status 2 (or any value that is neither 0 nor 1) must be disambiguated before calling it an
    // error: if at least one environ is readable the probe did run and found nothing, so report the
    // none marker; only when no environ file is readable at all is the probe genuinely unable to
    // answer, so report the error marker. The readability loop uses only bash builtins (glob, test,
    // break) so disambiguating costs no extra process. Single quotes protect the grep pattern; the
    // $f/$r references stay unquoted because the whole script is wrapped in double quotes for the
    // ProcessStartInfo argument, and an inner double quote would terminate that argument early.
    // The group-level stderr redirect keeps error text ("command not found", "No such file or
    // directory", grep's per-file "Permission denied") out of the agent log; GetProcessOutput
    // leaves the child's stderr inherited.
    private static readonly string DisplayProbeArguments =
        $"-c \"{{ grep -qz '^DISPLAY=' /proc/[0-9]*/environ; case $? in 0) echo {DisplayFoundMarker};; 1) echo {DisplayNoneMarker};; *) r=0; for f in /proc/[0-9]*/environ; do if [ -r $f ]; then r=1; break; fi; done; if [ $r = 1 ]; then echo {DisplayNoneMarker}; else echo {DisplayProbeErrorMarker}; fi;; esac; }} 2>/dev/null\"";

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

            // Check if we're running on Ubuntu Server (which typically doesn't have GUI packages)
            var ubuntuServerResult = await _processManager.GetProcessOutput("dpkg", "-l ubuntu-server", 3000);
            if (ubuntuServerResult.IsSuccess)
            {
                _logger.LogInformationDeduped("Ubuntu Server package detected - assuming headless environment.");
                return true;
            }

            // If we can't find any display server or GUI indicators, assume headless
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