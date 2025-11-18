using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.Libraries.DevicesCommon.Services;
using Microsoft.Extensions.Logging;

namespace ControlR.DesktopClient.Linux.Services;

internal class DesktopEnvironmentDetector(ILogger<DesktopEnvironmentDetector> logger) : IDesktopEnvironmentDetector
{
  private readonly ILogger<DesktopEnvironmentDetector> _logger = logger;
  private DesktopEnvironmentType? _cachedType;

  public static DesktopEnvironmentDetector Instance {get;} = 
    new DesktopEnvironmentDetector(
      new SerilogLogger<DesktopEnvironmentDetector>());

  public DesktopEnvironmentType GetDesktopEnvironment()
  {
    if (_cachedType.HasValue)
    {
      return _cachedType.Value;
    }

    // Check WAYLAND_DISPLAY environment variable first
    var waylandDisplay = Environment.GetEnvironmentVariable("WAYLAND_DISPLAY");
    if (!string.IsNullOrWhiteSpace(waylandDisplay))
    {
      _logger.LogInformation("Detected Wayland session (WAYLAND_DISPLAY={WaylandDisplay})", waylandDisplay);
      _cachedType = DesktopEnvironmentType.Wayland;
      return _cachedType.Value;
    }

    // Check DISPLAY environment variable for X11
    var display = Environment.GetEnvironmentVariable("DISPLAY");
    if (!string.IsNullOrWhiteSpace(display))
    {
      // Check if XWayland (running Wayland with X11 compatibility)
      var xdgSessionType = Environment.GetEnvironmentVariable("XDG_SESSION_TYPE");
      if (xdgSessionType?.Equals("wayland", StringComparison.OrdinalIgnoreCase) == true)
      {
        _logger.LogInformation("Detected XWayland session (DISPLAY={Display}, XDG_SESSION_TYPE=wayland)", display);
        _cachedType = DesktopEnvironmentType.Wayland;
        return _cachedType.Value;
      }

      _logger.LogInformation("Detected X11 session (DISPLAY={Display})", display);
      _cachedType = DesktopEnvironmentType.X11;
      return _cachedType.Value;
    }

    // Fallback: check XDG_SESSION_TYPE
    var sessionType = Environment.GetEnvironmentVariable("XDG_SESSION_TYPE");
    if (sessionType?.Equals("wayland", StringComparison.OrdinalIgnoreCase) == true)
    {
      _logger.LogWarning("Detected Wayland session via XDG_SESSION_TYPE, but WAYLAND_DISPLAY not set");
      _cachedType = DesktopEnvironmentType.Wayland;
      return _cachedType.Value;
    }
    else if (sessionType?.Equals("x11", StringComparison.OrdinalIgnoreCase) == true)
    {
      _logger.LogWarning("Detected X11 session via XDG_SESSION_TYPE, but DISPLAY not set");
      _cachedType = DesktopEnvironmentType.X11;
      return _cachedType.Value;
    }

    _logger.LogWarning("Unable to detect desktop environment type. Defaulting to Unknown.");
    _cachedType = DesktopEnvironmentType.Unknown;
    return _cachedType.Value;
  }

  public bool IsWayland()
  {
    return GetDesktopEnvironment() == DesktopEnvironmentType.Wayland;
  }

  public bool IsX11()
  {
    return GetDesktopEnvironment() == DesktopEnvironmentType.X11;
  }
}
