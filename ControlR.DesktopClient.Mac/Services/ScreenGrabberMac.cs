using ControlR.DesktopClient.Common.Models;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.DesktopClient.Mac.Helpers;
using ControlR.Libraries.NativeInterop.Unix.MacOs;
using Microsoft.Extensions.Logging;
using SkiaSharp;

namespace ControlR.DesktopClient.Mac.Services;

public sealed class ScreenGrabberMac(
  IDisplayManager displayManager,
  ILogger<ScreenGrabberMac> logger) : IScreenGrabber
{
  private const string CoreGraphicsCaptureMode = "CoreGraphics";
  private readonly IDisplayManager _displayManager = displayManager;
  private readonly ILogger<ScreenGrabberMac> _logger = logger;

  public async Task<CaptureResult> CaptureAllDisplays(bool captureCursor = true)
  {
    try
    {
      return await CaptureAllDisplaysImpl(captureCursor);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error grabbing all screens.");
      return CaptureResult.Fail(ex);
    }
  }
  public async Task<CaptureResult> CaptureDisplay(
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

  public ValueTask DisposeAsync()
  {
    return ValueTask.CompletedTask;
  }

  public Task Initialize(CancellationToken cancellationToken)
  {
    return Task.CompletedTask;
  }

  private async Task<CaptureResult> CaptureAllDisplaysImpl(bool captureCursor)
  {
    try
    {
      var virtualBounds = await _displayManager.GetVirtualScreenBounds();

      if (virtualBounds.IsEmpty)
      {
        return CaptureResult.Fail("No displays found.");
      }

      var displays = await _displayManager.GetDisplays();
      if (displays.Count == 0)
      {
        return CaptureResult.Fail("No displays found.");
      }

      if (displays.Count == 1)
      {
        var displayIdResult = uint.TryParse(displays[0].DeviceName, out var displayId);
        if (!displayIdResult)
        {
          return CaptureResult.Fail($"Invalid display ID: {displays[0].DeviceName}");
        }

        var cgImageRef = CoreGraphics.CGDisplayCreateImage(displayId);
        if (cgImageRef == nint.Zero)
        {
          return CaptureResult.Fail("Failed to create display image.");
        }

        try
        {
          var bitmap = CoreGraphicsHelper.CGImageToSKBitmap(cgImageRef);
          if (bitmap is null)
          {
            return CaptureResult.Fail("Failed to convert CGImage to SKBitmap.");
          }

          if (captureCursor)
          {
            _logger.LogDebug("Cursor capture is not yet implemented on macOS.");
          }

          return CaptureResult.Ok(bitmap, captureMode: CoreGraphicsCaptureMode);
        }
        finally
        {
          CoreGraphicsHelper.ReleaseCGImage(cgImageRef);
        }
      }

      var compositeBitmap = new SKBitmap(virtualBounds.Width, virtualBounds.Height);
      using var canvas = new SKCanvas(compositeBitmap);
      canvas.Clear(SKColors.Black);

      foreach (var display in displays)
      {
        if (!uint.TryParse(display.DeviceName, out var displayId))
        {
          _logger.LogWarning("Invalid display ID: {DisplayId}", display.DeviceName);
          continue;
        }

        var cgImageRef = CoreGraphics.CGDisplayCreateImage(displayId);
        if (cgImageRef == nint.Zero)
        {
          _logger.LogWarning("Failed to create display image for {DisplayId}", display.DeviceName);
          continue;
        }

        try
        {
          var displayBitmap = CoreGraphicsHelper.CGImageToSKBitmap(cgImageRef);
          if (displayBitmap is null)
          {
            _logger.LogWarning("Failed to convert CGImage to SKBitmap for {DisplayId}", display.DeviceName);
            continue;
          }

          using (displayBitmap)
          {
            var destRect = SKRect.Create(
              display.MonitorArea.X - virtualBounds.X,
              display.MonitorArea.Y - virtualBounds.Y,
              display.MonitorArea.Width,
              display.MonitorArea.Height);

            canvas.DrawBitmap(displayBitmap, destRect);
          }
        }
        finally
        {
          CoreGraphicsHelper.ReleaseCGImage(cgImageRef);
        }
      }

      if (captureCursor)
      {
        _logger.LogDebug("Cursor capture is not yet implemented on macOS.");
      }

      return CaptureResult.Ok(compositeBitmap, captureMode: CoreGraphicsCaptureMode);
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

      return CaptureResult.Ok(bitmap, captureMode: CoreGraphicsCaptureMode);
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
