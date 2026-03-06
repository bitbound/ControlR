using System.Drawing;
using ControlR.DesktopClient.Common.Models;
using ControlR.DesktopClient.Common.Services;

namespace ControlR.DesktopClient.Tests.Services;

public class DisplayCoordinateConverterTests
{
  [Fact]
  public void DisplayPercentToLogical_UsesLogicalBounds()
  {
    var display = CreateDisplay(
      logicalBounds: new Rectangle(-100, 50, 1000, 500),
      physicalSize: new Size(2000, 1000),
      scaleFactor: 2.0);

    var result = DisplayCoordinateConverter.DisplayPercentToLogical(0.1, 0.8, display);

    Assert.Equal(0, result.X);
    Assert.Equal(450, result.Y);
  }

  [Fact]
  public void DisplayPercentToPhysical_ClampsOutOfRangeInput()
  {
    var display = CreateDisplay(
      logicalBounds: new Rectangle(0, 0, 100, 100),
      physicalSize: new Size(300, 200),
      scaleFactor: 3.0);

    var result = DisplayCoordinateConverter.DisplayPercentToPhysical(2.0, -1.0, display);

    Assert.Equal(299, result.X);
    Assert.Equal(0, result.Y);
  }

  [Fact]
  public void DisplayPercentToPhysical_UsesPhysicalSize()
  {
    var display = CreateDisplay(
      logicalBounds: new Rectangle(50, 100, 400, 300),
      physicalSize: new Size(800, 600),
      scaleFactor: 2.0);

    var result = DisplayCoordinateConverter.DisplayPercentToPhysical(0.5, 0.25, display);

    Assert.Equal(400, result.X);
    Assert.Equal(150, result.Y);
  }

  private static DisplayInfo CreateDisplay(Rectangle logicalBounds, Size physicalSize, double scaleFactor)
  {
    return new DisplayInfo
    {
      DeviceName = "display-0",
      DisplayName = "Display 0",
      Index = 0,
      IsPrimary = true,
      LogicalMonitorArea = logicalBounds,
      PhysicalSize = physicalSize,
      ScaleFactor = scaleFactor,
    };
  }
}
