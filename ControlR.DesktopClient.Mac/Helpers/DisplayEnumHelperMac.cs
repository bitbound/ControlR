using ControlR.DesktopClient.Common.Models;
using ControlR.Libraries.NativeInterop.Unix.MacOs;
using System.Drawing;

namespace ControlR.DesktopClient.Mac.Helpers;

internal static class DisplayEnumHelperMac
{
  private const uint MaxDisplays = 32;

  public static List<DisplayInfo> GetDisplays()
  {
    var displays = new List<DisplayInfo>();

    try
    {
      var displayIds = new uint[MaxDisplays];
      var result = CoreGraphics.CGGetOnlineDisplayList(MaxDisplays, displayIds, out var displayCount);

      if (result != 0 || displayCount == 0)
      {
        // Fallback to main display only
        Console.WriteLine($"DisplaysEnumerationHelper: Using fallback, result={result}, displayCount={displayCount}");
        var mainDisplayId = CoreGraphics.CGMainDisplayID();
        return [CreateDisplayInfo(mainDisplayId, 0, true)];
      }

      Console.WriteLine($"DisplaysEnumerationHelper: Found {displayCount} displays");
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
      Console.WriteLine($"DisplaysEnumerationHelper: Exception occurred: {ex.Message}");
      var mainDisplayId = CoreGraphics.CGMainDisplayID();
      displays.Add(CreateDisplayInfo(mainDisplayId, 0, true));
    }

    return displays;
  }

  private static DisplayInfo CreateDisplayInfo(uint displayId, int index, bool isMain)
  {
    var bounds = CoreGraphics.CGDisplayBounds(displayId);
    var logicalWidth = (int)bounds.Width;
    var logicalHeight = (int)bounds.Height;
    
    // On macOS, CGDisplayPixelsWide/High return logical dimensions, not actual pixels
    // To get actual pixel dimensions, we need to capture the display and check the image size
    // Or use the backing scale factor. Let's try capturing to get the real dimensions.
    nint testImageRef = nint.Zero;
    int actualPixelWidth = logicalWidth;
    int actualPixelHeight = logicalHeight;
    double scaleFactor = 1.0;
    
    try
    {
      // Create a test capture to get actual pixel dimensions
      testImageRef = CoreGraphics.CGDisplayCreateImage(displayId);
      if (testImageRef != nint.Zero)
      {
        actualPixelWidth = (int)CoreGraphics.CGImageGetWidth(testImageRef);
        actualPixelHeight = (int)CoreGraphics.CGImageGetHeight(testImageRef);
        
        // Calculate the actual scale factor
        scaleFactor = Math.Max(
          (double)actualPixelWidth / logicalWidth,
          (double)actualPixelHeight / logicalHeight);
      }
    }
    catch
    {
      // If we can't capture, fall back to logical dimensions
      actualPixelWidth = logicalWidth;
      actualPixelHeight = logicalHeight;
      scaleFactor = 1.0;
    }
    finally
    {
      if (testImageRef != nint.Zero)
      {
        CoreGraphics.CFRelease(testImageRef);
      }
    }

    // The captured image will be in pixel coordinates starting from (0,0) for each display
    // But the MonitorArea should reflect the actual screen area for coordinate calculations
    var monitorArea = new Rectangle(
      (int)(bounds.X * scaleFactor), // Scale logical position to pixel position
      (int)(bounds.Y * scaleFactor), // Scale logical position to pixel position
      actualPixelWidth, 
      actualPixelHeight);

    // Debug logging - this will help identify coordinate mismatches
    Console.WriteLine($"Display {displayId}: Logical bounds={logicalWidth}x{logicalHeight} at ({bounds.X},{bounds.Y}), " +
                      $"Actual pixel size={actualPixelWidth}x{actualPixelHeight}, Scale={scaleFactor:F2}, " +
                      $"MonitorArea={monitorArea.Width}x{monitorArea.Height} at ({monitorArea.X},{monitorArea.Y})");

    return new DisplayInfo
    {
      DeviceName = displayId.ToString(),
      DisplayName = isMain ? "Main Display" : $"Display {index + 1}",
      IsPrimary = isMain,
      MonitorArea = monitorArea,
      WorkArea = monitorArea,
      ScaleFactor = scaleFactor
    };
  }
}
