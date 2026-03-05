using ControlR.DesktopClient.Common.Models;
using ControlR.Libraries.NativeInterop.Unix.MacOs;
using Microsoft.Extensions.Logging;
using System.Drawing;

namespace ControlR.DesktopClient.Mac.Helpers;

internal interface IDisplayEnumHelperMac
{
  List<DisplayInfo> GetDisplays();
}

internal class DisplayEnumHelperMac(ILogger<DisplayEnumHelperMac> logger) : IDisplayEnumHelperMac
{
  private const uint MaxDisplays = 32;
  private readonly ILogger<DisplayEnumHelperMac> _logger = logger;

  public List<DisplayInfo> GetDisplays()
  {
    var displays = new List<DisplayInfo>();

    try
    {
      var displayIds = new uint[MaxDisplays];
      var result = CoreGraphics.CGGetOnlineDisplayList(MaxDisplays, displayIds, out var displayCount);

      if (result != 0 || displayCount == 0)
      {
        // Fallback to main display only
        _logger.LogWarning("DisplayEnumHelperMac: Using fallback, result={Result}, displayCount={Count}", result, displayCount);
        var mainDisplayId = CoreGraphics.CGMainDisplayID();
        return [CreateDisplayInfo(mainDisplayId, 0, true)];
      }

      _logger.LogDebug("DisplayEnumHelperMac: Found {Count} displays", displayCount);
      for (var i = 0; i < displayCount; i++)
      {
        var displayId = displayIds[i];
        var isMain = CoreGraphics.CGDisplayIsMain(displayId);
        var displayInfo = CreateDisplayInfo(displayId, i, isMain);
        displays.Add(displayInfo);
      }
    }
    catch (Exception ex)
    {
      // Fallback to main display only
      _logger.LogError(ex, "DisplayEnumHelperMac: Exception occurred while enumerating displays");
      var mainDisplayId = CoreGraphics.CGMainDisplayID();
      displays.Add(CreateDisplayInfo(mainDisplayId, 0, true));
    }

    return displays;
  }

  private DisplayInfo CreateDisplayInfo(uint displayId, int index, bool isMain)
  {
    var bounds = CoreGraphics.CGDisplayBounds(displayId);
    var logicalWidth = (int)bounds.Width;
    var logicalHeight = (int)bounds.Height;

    // On macOS, CGDisplayPixelsWide/High return logical dimensions, not physical pixels.
    // To get pixel dimensions, we need to capture the display and check the image size.
    nint testImageRef = nint.Zero;
    int pixelWidth = logicalWidth;
    int pixelHeight = logicalHeight;
    double scaleFactor = 1.0;

    try
    {
      // Create a test capture to get actual pixel dimensions
      testImageRef = CoreGraphics.CGDisplayCreateImage(displayId);
      if (testImageRef != nint.Zero)
      {
        pixelWidth = (int)CoreGraphics.CGImageGetWidth(testImageRef);
        pixelHeight = (int)CoreGraphics.CGImageGetHeight(testImageRef);

        // Calculate the backing scale factor (physical / logical)
        scaleFactor = Math.Max(
          (double)pixelWidth / logicalWidth,
          (double)pixelHeight / logicalHeight);
      }
    }
    catch
    {
      // If we can't capture, fall back to logical dimensions
      pixelWidth = logicalWidth;
      pixelHeight = logicalHeight;
      scaleFactor = 1.0;
    }
    finally
    {
      if (testImageRef != nint.Zero)
      {
        CoreGraphics.CFRelease(testImageRef);
      }
    }

    var logicalArea = new Rectangle(
      (int)bounds.X,
      (int)bounds.Y,
      logicalWidth,
      logicalHeight);

    _logger.LogDebug(
      "Display {DisplayId}: Logical bounds={LogicalW}x{LogicalH} at ({X},{Y}), Physical pixel size={PhysW}x{PhysH}, Scale={Scale:F2}",
      displayId, logicalWidth, logicalHeight, bounds.X, bounds.Y, pixelWidth, pixelHeight, scaleFactor);

    return new DisplayInfo
    {
      DeviceName = displayId.ToString(),
      DisplayName = $"Display {index + 1}",
      Index = index,
      IsPrimary = isMain,
      PhysicalSize = new Size(pixelWidth, pixelHeight),
      LogicalMonitorArea = logicalArea,
      ScaleFactor = scaleFactor
    };
  }
}
