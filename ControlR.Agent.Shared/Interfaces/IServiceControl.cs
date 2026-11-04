namespace ControlR.Agent.Shared.Interfaces;

/// <summary>
/// Provides methods to control ControlR services on the current platform.
/// </summary>
public interface IServiceControl
{
    /// <summary>
    /// Makes each logged-in user's own service manager re-read its unit files.
    /// On Linux: Runs systemctl --user daemon-reload per user. The desktop client unit is loaded by
    /// that user's manager, not the system one, so an install that rewrote the unit in place leaves
    /// the manager holding the definition it read before the rewrite.
    /// On Mac: Throws NotSupportedException. The Mac install boots the job out and bootstraps it
    /// again from the plist, which reads the file fresh.
    /// On Windows: Throws NotSupportedException. The service registration is read at start.
    /// </summary>
    /// <exception cref="NotSupportedException">Thrown on Mac and Windows platforms.</exception>
    Task ReloadUserDaemons(bool throwOnFailure);

    /// <summary>
    /// Starts the main ControlR agent service.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task StartAgentService(bool throwOnFailure);

    /// <summary>
    /// Starts the ControlR desktop client service.
    /// On Mac: Starts the LaunchAgent for the desktop client.
    /// On Linux: Starts the systemd user service for the desktop client.
    /// On Windows: Throws NotSupportedException as desktop client is handled differently.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="NotSupportedException">Thrown on Windows platform.</exception>
    Task StartDesktopClientService(bool throwOnFailure);

    /// <summary>
    /// Stops the main ControlR agent service.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task StopAgentService(bool throwOnFailure);

    /// <summary>
    /// Stops the ControlR desktop client service.
    /// On Mac: Stops the LaunchAgent for the desktop client.
    /// On Linux: Stops the systemd user service for the desktop client.
    /// On Windows: Throws NotSupportedException as desktop client is handled differently.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="NotSupportedException">Thrown on Windows platform.</exception>
    Task StopDesktopClientService(bool throwOnFailure);

    /// <summary>
    /// Stops the desktop client user service of an install that a brand migration is replacing, for
    /// all logged-in users. The unit is named for the replaced install's own brand and instance id,
    /// so stopping the current brand's unit does not reach it. Only the Linux migration path calls
    /// this, because it is the only one that kills the replaced install's client processes; the units
    /// restart on failure and would relaunch them.
    /// On Mac: Throws NotSupportedException; the Mac migration path has no client kill to precede.
    /// On Windows: Throws NotSupportedException as desktop client is handled differently.
    /// </summary>
    /// <exception cref="NotSupportedException">Thrown on Windows and Mac platforms.</exception>
    Task StopPreviousBrandDesktopClientService(string previousBrandName, string? previousInstanceId, bool throwOnFailure);
}
