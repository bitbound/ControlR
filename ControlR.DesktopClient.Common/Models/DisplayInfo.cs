using System.Drawing;
using ControlR.Libraries.Api.Contracts.Enums;

namespace ControlR.DesktopClient.Common.Models;

public class DisplayInfo
{
  /// <summary>
  /// The pixel dimensions of the actual capture frame for this display.
  /// </summary>
  public Size CapturePixelSize { get; init; }
  public double CapturePixelsPerLayoutUnit => GetPixelsPerLayoutUnit(CapturePixelSize);
  public required string DeviceName { get; init; }
  public string DisplayName { get; set; } = string.Empty;
  public int Index { get; set; }
  public bool IsPrimary { get; init; }
  /// <summary>
  /// Monitor bounds used for display layout topology. The coordinate space is described
  /// by <see cref="LayoutCoordinateSpace"/> because platforms do not all expose layout
  /// bounds in the same units.
  /// </summary>
  public Rectangle LayoutBounds { get; init; }
  public DisplayLayoutCoordinateSpace LayoutCoordinateSpace { get; init; }
  /// <summary>
  /// Optional backing-store or hardware-native pixel dimensions when they differ from the
  /// capture frame size and the platform can report them honestly.
  /// </summary>
  public Size? NativePixelSize { get; init; }
  public double NativePixelsPerLayoutUnit => GetPixelsPerLayoutUnit(NativePixelSize ?? CapturePixelSize);

  private double GetPixelsPerLayoutUnit(Size pixelSize)
  {
    if (LayoutBounds.Width <= 0 || LayoutBounds.Height <= 0)
    {
      return 1.0;
    }

    var widthRatio = (double)pixelSize.Width / LayoutBounds.Width;
    var heightRatio = (double)pixelSize.Height / LayoutBounds.Height;

    return Math.Max(widthRatio, heightRatio);
  }
}