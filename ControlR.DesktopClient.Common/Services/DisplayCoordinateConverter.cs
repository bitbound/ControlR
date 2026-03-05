using System.Drawing;
using ControlR.DesktopClient.Common.Models;
using ControlR.Libraries.Shared.Primitives;

namespace ControlR.DesktopClient.Common.Services;

public static class DisplayCoordinateConverter
{
  public static LogicalPoint DisplayPercentToLogical(double percentOfDisplayX, double percentOfDisplayY, DisplayInfo display)
  {
    var clampedX = Math.Clamp(percentOfDisplayX, 0, 1);
    var clampedY = Math.Clamp(percentOfDisplayY, 0, 1);

    var bounds = display.LogicalMonitorArea;
    var absoluteX = bounds.Left + bounds.Width * clampedX;
    var absoluteY = bounds.Top + bounds.Height * clampedY;

    return new LogicalPoint(absoluteX, absoluteY).Clamp(ToLogicalRect(bounds));
  }

  /// <summary>
  /// Returns a physical pixel coordinate relative to the display's own top-left corner (0, 0).
  /// Use this for stream-based injection (e.g. Wayland NotifyPointerMotionAbsolute) where
  /// the coordinate space is per-stream. For platforms that need absolute screen coordinates,
  /// override this in the platform-specific display manager.
  /// </summary>
  public static PhysicalPoint DisplayPercentToPhysical(double percentOfDisplayX, double percentOfDisplayY, DisplayInfo display)
  {
    var clampedX = Math.Clamp(percentOfDisplayX, 0, 1);
    var clampedY = Math.Clamp(percentOfDisplayY, 0, 1);

    var maxX = Math.Max(0, display.PhysicalSize.Width - 1);
    var maxY = Math.Max(0, display.PhysicalSize.Height - 1);

    var x = (int)Math.Round(maxX * clampedX);
    var y = (int)Math.Round(maxY * clampedY);

    x = Math.Clamp(x, 0, maxX);
    y = Math.Clamp(y, 0, maxY);

    return new PhysicalPoint(x, y);
  }

  public static LogicalRect ToLogicalRect(Rectangle rectangle)
  {
    return new LogicalRect(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);
  }
}
