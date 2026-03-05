using System.Drawing;
using System.Numerics;

namespace ControlR.DesktopClient.Common.Models;

public class DisplayInfo
{
  public required string DeviceName { get; init; }
  public string DisplayName { get; set; } = string.Empty;
  public int Index { get; set; }
  public bool IsPrimary { get; init; }

  /// <summary>
  /// Monitor bounds in logical (device-independent) units as reported by the OS/compositor.
  /// This is the authoritative source for display layout topology — position and size
  /// in a consistent coordinate space shared across all monitors.
  /// </summary>
  public Rectangle LogicalMonitorArea { get; init; }

  /// <summary>
  /// The physical pixel dimensions of the monitor's capture buffer (width and height only).
  /// No X/Y origin is stored here because a global physical origin is not reliably knowable
  /// on all platforms (e.g. Wayland and macOS with mixed DPI multi-monitor setups).
  /// Use <see cref="LogicalMonitorArea"/> for layout; use this for frame/buffer sizes.
  /// </summary>
  public Size PhysicalSize { get; init; }

  /// <summary>
  /// Scale factor: PhysicalSize / logical size.  physical = logical * ScaleFactor.
  /// </summary>
  public double ScaleFactor { get; set; } = 1;
}
