using System.Runtime.InteropServices;
using ControlR.DesktopClient.Common.Models;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.Libraries.NativeInterop.Unix.Linux;
using Microsoft.Extensions.Logging;
using SkiaSharp;

namespace ControlR.DesktopClient.Linux.Services;

internal class ScreenGrabberX11(
  IDisplayManager displayManager,
  ILogger<ScreenGrabberX11> logger) : IScreenGrabber
{
  private readonly IDisplayManager _displayManager = displayManager;
  private readonly ILogger<ScreenGrabberX11> _logger = logger;

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
  public Task Deinitialize(CancellationToken cancellationToken)
  {
    return Task.CompletedTask;
  }
  public Task Initialize(CancellationToken cancellationToken)
  {
    return Task.CompletedTask;
  }

  private static unsafe void ConvertRgb24ToBgra32Unsafe(byte* src, byte* dst, int width, int height, int srcBytesPerLine)
  {
    for (var y = 0; y < height; y++)
    {
      var srcLine = src + y * srcBytesPerLine;
      var dstLine = dst + y * width * 4;

      for (var x = 0; x < width; x++)
      {
        var srcPixel = srcLine + x * 3;
        var dstPixel = dstLine + x * 4;

        // X11 typically uses BGR order, convert to BGRA
        dstPixel[0] = srcPixel[0]; // B
        dstPixel[1] = srcPixel[1]; // G
        dstPixel[2] = srcPixel[2]; // R
        dstPixel[3] = 255;         // A
      }
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

      // For multiple displays, capture the root window which includes all screens
      var xDisplay = LibX11.XOpenDisplay("");
      if (xDisplay == nint.Zero)
      {
        return CaptureResult.Fail("Failed to open X11 display.");
      }

      try
      {
        var rootWindow = LibX11.XDefaultRootWindow(xDisplay);
        var bitmap = CaptureWindow(xDisplay, rootWindow, virtualBounds.Width, virtualBounds.Height);

        if (bitmap is null)
        {
          return CaptureResult.Fail("Failed to capture root window.");
        }

        // Note: captureCursor implementation would require additional cursor APIs
        if (captureCursor)
        {
          _logger.LogDebug("Cursor capture is not yet implemented on Linux/X11.");
        }

        return CaptureResult.Ok(bitmap, isUsingGpu: false);
      }
      finally
      {
        LibX11.XCloseDisplay(xDisplay);
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
    try
    {
      var xDisplay = LibX11.XOpenDisplay("");
      if (xDisplay == nint.Zero)
      {
        return CaptureResult.Fail("Failed to open X11 display.");
      }

      try
      {
        var rootWindow = LibX11.XDefaultRootWindow(xDisplay);
        var bitmap = CaptureWindow(xDisplay, rootWindow, display.MonitorArea.Width, display.MonitorArea.Height,
                                  display.MonitorArea.X, display.MonitorArea.Y);

        if (bitmap is null)
        {
          return CaptureResult.Fail("Failed to capture display window.");
        }

        // Note: captureCursor implementation would require additional cursor APIs
        if (captureCursor)
        {
          _logger.LogDebug("Cursor capture is not yet implemented on Linux/X11.");
        }

        return CaptureResult.Ok(bitmap, isUsingGpu: false);
      }
      finally
      {
        LibX11.XCloseDisplay(xDisplay);
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error capturing display {DisplayId}.", display.DeviceName);
      return CaptureResult.Fail(ex);
    }
  }
  private SKBitmap? CaptureWindow(nint display, nint window, int width, int height, int x = 0, int y = 0)
  {
    try
    {
      // Capture the image from X11
      var xImage = LibX11.XGetImage(display, window, x, y, width, height, 0xFFFFFFFF, 2); // ZPixmap format

      if (xImage == nint.Zero)
      {
        _logger.LogError("Failed to get X11 image.");
        return null;
      }

      try
      {
        var imageStruct = Marshal.PtrToStructure<LibX11.XImage>(xImage);

        // Create SKBitmap from X11 image data
        var bitmap = new SKBitmap(imageStruct.width, imageStruct.height, SKColorType.Bgra8888, SKAlphaType.Opaque);
        var pixels = bitmap.GetPixels();

        // Copy image data
        unsafe
        {
          var srcPtr = (byte*)imageStruct.data;
          var dstPtr = (byte*)pixels;
          var bytesToCopy = imageStruct.height * imageStruct.bytes_per_line;

          // Handle different bit depths and formats
          if (imageStruct.bits_per_pixel == 32)
          {
            // Direct copy for 32-bit
            Buffer.MemoryCopy(srcPtr, dstPtr, bitmap.ByteCount, Math.Min(bytesToCopy, bitmap.ByteCount));
          }
          else if (imageStruct.bits_per_pixel == 24)
          {
            // Convert 24-bit to 32-bit
            ConvertRgb24ToBgra32Unsafe(srcPtr, dstPtr, imageStruct.width, imageStruct.height, imageStruct.bytes_per_line);
          }
          else
          {
            _logger.LogWarning("Unsupported bit depth: {BitDepth}", imageStruct.bits_per_pixel);
            bitmap.Dispose();
            return null;
          }
        }

        return bitmap;
      }
      finally
      {
        LibX11.XDestroyImage(xImage);
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error capturing X11 window.");
      return null;
    }
  }
}