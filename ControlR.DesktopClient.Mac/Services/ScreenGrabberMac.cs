using System.Collections.Concurrent;
using System.Drawing;
using ControlR.DesktopClient.Common.Extensions;
using ControlR.DesktopClient.Common.Models;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.DesktopClient.Common.Services;
using ControlR.DesktopClient.Mac.Helpers;
using ControlR.Libraries.NativeInterop.Unix.MacOs;
using Microsoft.Extensions.Logging;
using SkiaSharp;

namespace ControlR.DesktopClient.Mac.Services;

public class ScreenGrabberMac(
  ILogger<ScreenGrabberMac> logger,
  IImageUtility imageUtility) : IScreenGrabber
{
  private const string AllDisplaysKey = "__ALL__";
  private readonly IImageUtility _imageUtility = imageUtility;
  private readonly ConcurrentDictionary<string, SKBitmap> _lastFrames = new();
  private readonly ILogger<ScreenGrabberMac> _logger = logger;

  public CaptureResult Capture(
    DisplayInfo targetDisplay,
    bool captureCursor = true)
  {
    try
    {
      return CaptureDisplay(targetDisplay, captureCursor);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error grabbing screen for display {DisplayName}.", targetDisplay.DeviceName);
      return CaptureResult.Fail(ex);
    }
  }

  public CaptureResult Capture(bool captureCursor = true)
  {
    try
    {
      return CaptureAllDisplays(captureCursor);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error grabbing all screens.");
      return CaptureResult.Fail(ex);
    }
  }

  public IEnumerable<DisplayInfo> GetDisplays()
  {
    return DisplaysEnumerationHelper.GetDisplays();
  }

  public Rectangle GetVirtualScreenBounds()
  {
    try
    {
      var displays = GetDisplays().ToList();
      if (displays.Count == 0)
      {
        return Rectangle.Empty;
      }

      var minX = displays.Min(d => d.MonitorArea.Left);
      var minY = displays.Min(d => d.MonitorArea.Top);
      var maxX = displays.Max(d => d.MonitorArea.Right);
      var maxY = displays.Max(d => d.MonitorArea.Bottom);

      return new Rectangle(minX, minY, maxX - minX, maxY - minY);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error getting virtual screen bounds.");
      // Return main display bounds as fallback
      var mainDisplayId = CoreGraphics.CGMainDisplayID();
      var bounds = CoreGraphics.CGDisplayBounds(mainDisplayId);
      return bounds.ToRectangle();
    }
  }

  private CaptureResult CaptureAllDisplays(bool captureCursor)
  {
    try
    {
      var virtualBounds = GetVirtualScreenBounds();

      if (virtualBounds.IsEmpty)
      {
        return CaptureResult.Fail("No displays found.");
      }

      // For multiple displays, we'll capture the main display only for now
      // A proper implementation would need to composite all displays into a single image
      var mainDisplayId = CoreGraphics.CGMainDisplayID();
      var cgImageRef = CoreGraphics.CGDisplayCreateImage(mainDisplayId);

      if (cgImageRef == nint.Zero)
      {
        return CaptureResult.Fail("Failed to create main display image.");
      }

      try
      {
        var bitmap = CoreGraphicsHelper.CGImageToSKBitmap(cgImageRef);

        if (bitmap is null)
        {
          return CaptureResult.Fail("Failed to convert CGImage to SKBitmap.");
        }

        // Note: captureCursor is ignored for now
        if (captureCursor)
        {
          _logger.LogDebug("Cursor capture is not yet implemented on macOS.");
        }

        var key = AllDisplaysKey;
        Rectangle[] dirtyRects = [];
        if (_lastFrames.TryGetValue(key, out var previous))
        {
          try
          {
            var diff = _imageUtility.GetChangedArea(bitmap, previous);
            if (diff.IsSuccess && !diff.Value.IsEmpty)
            {
              dirtyRects =
              [
                 diff.Value.ToRectangle()
              ];
            }
          }
          catch (Exception ex)
          {
            _logger.LogDebug(ex, "Failed to compute dirty rect diff for macOS virtual capture.");
          }
        }

        if (dirtyRects.Length == 0)
        {
          dirtyRects = [new Rectangle(0, 0, bitmap.Width, bitmap.Height)];
        }

        var copy = bitmap.Copy();
        _lastFrames.AddOrUpdate(key, copy, (_, old) => { old.Dispose(); return copy; });
        return CaptureResult.Ok(bitmap, isUsingGpu: false, dirtyRects: dirtyRects);
      }
      finally
      {
        CoreGraphicsHelper.ReleaseCGImage(cgImageRef);
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error capturing all displays.");
      return CaptureResult.Fail(ex);
    }
  }

  private CaptureResult CaptureDisplay(DisplayInfo display, bool captureCursor)
  {
    nint cgImageRef = nint.Zero;

    try
    {
      if (!uint.TryParse(display.DeviceName, out var displayId))
      {
        return CaptureResult.Fail($"Invalid display ID: {display.DeviceName}");
      }

      // Capture the entire display
      cgImageRef = CoreGraphics.CGDisplayCreateImage(displayId);

      if (cgImageRef == nint.Zero)
      {
        return CaptureResult.Fail("Failed to create display image.");
      }

      var bitmap = CoreGraphicsHelper.CGImageToSKBitmap(cgImageRef);

      if (bitmap is null)
      {
        return CaptureResult.Fail("Failed to convert CGImage to SKBitmap.");
      }

      // Note: captureCursor is ignored for now as drawing cursor on macOS requires additional APIs
      // This could be implemented using CGWindowListCreateImage with kCGWindowListOptionIncludingCursor
      if (captureCursor)
      {
        _logger.LogDebug("Cursor capture is not yet implemented on macOS.");
      }

      var key = display.DeviceName;
      Rectangle[] dirtyRects = [];
      if (_lastFrames.TryGetValue(key, out var previous))
      {
        try
        {
          var diff = _imageUtility.GetChangedArea(bitmap, previous);
          if (diff.IsSuccess && !diff.Value.IsEmpty)
          {
            dirtyRects =
            [
              diff.Value.ToRectangle()
            ];
          }
        }
        catch (Exception ex)
        {
          _logger.LogDebug(ex, "Failed to compute dirty rect diff for macOS capture.");
        }
      }
      
      if (dirtyRects.Length == 0)
      {
        dirtyRects = [new Rectangle(0, 0, bitmap.Width, bitmap.Height)];
      }

      var copy = bitmap.Copy();
      _lastFrames.AddOrUpdate(key, copy, (_, old) => { old.Dispose(); return copy; });
      return CaptureResult.Ok(bitmap, isUsingGpu: false, dirtyRects: dirtyRects);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error capturing display {DisplayId}.", display.DeviceName);
      return CaptureResult.Fail(ex);
    }
    finally
    {
      if (cgImageRef != nint.Zero)
      {
        CoreGraphicsHelper.ReleaseCGImage(cgImageRef);
      }
    }
  }
}
