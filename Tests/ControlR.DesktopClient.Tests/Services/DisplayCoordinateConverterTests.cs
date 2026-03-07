using System.Drawing;
using ControlR.DesktopClient.Common.Models;
using ControlR.DesktopClient.Common.Services;
using ControlR.Libraries.Api.Contracts.Enums;

namespace ControlR.DesktopClient.Tests.Services;

public class DisplayCoordinateConverterTests
{
  [Fact]
  public void DisplayPercentToCapturePoint_ClampsOutOfRangeInput()
  {
    var display = CreateDisplay(
      logicalBounds: new Rectangle(0, 0, 100, 100),
      capturePixelSize: new Size(300, 200),
      layoutCoordinateSpace: DisplayLayoutCoordinateSpace.Logical);

    var result = DisplayCoordinateConverter.DisplayPercentToCapturePoint(2.0, -1.0, display);

    Assert.Equal(299, result.X);
    Assert.Equal(0, result.Y);
  }

  [Fact]
  public void DisplayPercentToCapturePoint_UsesCapturePixelSize()
  {
    var display = CreateDisplay(
      logicalBounds: new Rectangle(50, 100, 400, 300),
      capturePixelSize: new Size(800, 600),
      layoutCoordinateSpace: DisplayLayoutCoordinateSpace.Logical);

    var result = DisplayCoordinateConverter.DisplayPercentToCapturePoint(0.5, 0.25, display);

    Assert.Equal(400, result.X);
    Assert.Equal(150, result.Y);
  }

  [Fact]
  public void DisplayPercentToLayoutPoint_UsesLayoutBounds()
  {
    var display = CreateDisplay(
      logicalBounds: new Rectangle(-100, 50, 1000, 500),
      capturePixelSize: new Size(2000, 1000),
      layoutCoordinateSpace: DisplayLayoutCoordinateSpace.Logical);

    var result = DisplayCoordinateConverter.DisplayPercentToLayoutPoint(0.1, 0.8, display);

    Assert.Equal(0, result.X);
    Assert.Equal(450, result.Y);
  }

  private static DisplayInfo CreateDisplay(Rectangle logicalBounds, Size capturePixelSize, DisplayLayoutCoordinateSpace layoutCoordinateSpace)
  {
    return new DisplayInfo
    {
      DeviceName = "display-0",
      DisplayName = "Display 0",
      Index = 0,
      IsPrimary = true,
      LayoutBounds = logicalBounds,
      LayoutCoordinateSpace = layoutCoordinateSpace,
      CapturePixelSize = capturePixelSize,
    };
  }
}
