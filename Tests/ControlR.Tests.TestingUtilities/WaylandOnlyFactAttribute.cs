using Xunit;

namespace ControlR.Tests.TestingUtilities;

/// <summary>
/// Xunit attribute to skip tests when not running on a Wayland desktop session.
/// </summary>
public class WaylandOnlyFactAttribute : FactAttribute
{
  public WaylandOnlyFactAttribute()
  {
    if (!IsWaylandSession())
    {
      Skip = "Test only runs on Wayland desktop sessions";
    }
  }

  private static bool IsWaylandSession()
  {
    var waylandDisplay = Environment.GetEnvironmentVariable("WAYLAND_DISPLAY");
    var sessionType = Environment.GetEnvironmentVariable("XDG_SESSION_TYPE");

    return !string.IsNullOrEmpty(waylandDisplay) || 
           string.Equals(sessionType, "wayland", StringComparison.OrdinalIgnoreCase);
  }
}
