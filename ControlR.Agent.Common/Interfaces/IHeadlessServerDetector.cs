namespace ControlR.Agent.Common.Interfaces;

public interface IHeadlessServerDetector
{
    /// <summary>
    /// Determines if the current system is running in a headless environment (without X11 or Wayland display server).
    /// </summary>
    /// <returns>True if the system is headless, false if a display server is available.</returns>
    Task<bool> IsHeadlessServer();
}