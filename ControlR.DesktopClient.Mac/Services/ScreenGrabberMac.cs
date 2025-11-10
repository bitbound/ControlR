using ControlR.DesktopClient.Common.Models;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.DesktopClient.Mac.Helpers;
using ControlR.Libraries.NativeInterop.Unix.MacOs;
using Microsoft.Extensions.Logging;

namespace ControlR.DesktopClient.Mac.Services;

public class ScreenGrabberMac(
  IDisplayManager displayManager,
  ILogger<ScreenGrabberMac> logger) : IScreenGrabber
{
  private readonly IDisplayManager _displayManager = displayManager;
  private readonly ILogger<ScreenGrabberMac> _logger = logger;

  public CaptureResult CaptureAllDisplays(bool captureCursor = true)
  {
    try
    {
      return CaptureAllDisplaysImpl(captureCursor);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error grabbing all screens.");
      return CaptureResult.Fail(ex);
    }
  }

  public CaptureResult CaptureDisplay(
    DisplayInfo targetDisplay,
    bool captureCursor = true,
    bool forceKeyFrame = false)
  {
    try
    {
      return CaptureDisplayImpl(targetDisplay, captureCursor);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error grabbing screen for display {DisplayName}.", targetDisplay.DeviceName);
      return CaptureResult.Fail(ex);
    }
  }

  private CaptureResult CaptureAllDisplaysImpl(bool captureCursor)
  {
    try
    {
      var virtualBounds = _displayManager.GetVirtualScreenBounds();

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

        return CaptureResult.Ok(bitmap, isUsingGpu: false);
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

  private CaptureResult CaptureDisplayImpl(DisplayInfo display, bool captureCursor)
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

      return CaptureResult.Ok(bitmap, isUsingGpu: false);
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
