using ControlR.DesktopClient.Common.Models;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.DesktopClient.Mac.Helpers;
using ControlR.Libraries.NativeInterop.Mac;
using ControlR.Libraries.Shared.Primitives;
using Microsoft.Extensions.Logging;
using SkiaSharp;
using System.Drawing;

namespace ControlR.DesktopClient.Mac.Services;

public sealed class ScreenGrabberMac(
  IDisplayManager displayManager,
  IUiThread uiThread,
  ILogger<ScreenGrabberMac> logger) : IScreenGrabber
{
  private const string CoreGraphicsCaptureMode = "CoreGraphics";

  private readonly IDisplayManager _displayManager = displayManager;
  private readonly ILogger<ScreenGrabberMac> _logger = logger;
  private readonly IUiThread _uiThread = uiThread;

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
            var captureArea = displays[0].LayoutBounds;
            DrawCursorOnBitmap(bitmap, captureArea, displays);
          }

          return CaptureResult.Ok(bitmap, captureMode: CoreGraphicsCaptureMode);
        }
        finally
        {
          CoreGraphicsHelper.ReleaseCGImage(cgImageRef);
        }
      }

      var virtualBounds = await _displayManager.GetVirtualScreenLayoutBounds();

      if (virtualBounds.Width <= 0 || virtualBounds.Height <= 0)
      {
        return CaptureResult.Fail("No displays found.");
      }

      var compositeBitmap = new SKBitmap((int)virtualBounds.Width, (int)virtualBounds.Height);
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
          using var displayBitmap = CoreGraphicsHelper.CGImageToSKBitmap(cgImageRef);
          if (displayBitmap is null)
          {
            _logger.LogWarning("Failed to convert CGImage to SKBitmap for {DisplayId}", display.DeviceName);
            continue;
          }

          var destRect = SKRect.Create(
             (float)(display.LayoutBounds.X - virtualBounds.X),
             (float)(display.LayoutBounds.Y - virtualBounds.Y),
             display.LayoutBounds.Width,
             display.LayoutBounds.Height);

          canvas.DrawBitmap(displayBitmap, destRect);
        }
        finally
        {
          CoreGraphicsHelper.ReleaseCGImage(cgImageRef);
        }
      }

      if (captureCursor)
      {
        var boundsRect = new Rectangle((int)virtualBounds.X, (int)virtualBounds.Y, (int)virtualBounds.Width, (int)virtualBounds.Height);
        DrawCursorOnBitmap(compositeBitmap, boundsRect, displays);
      }

      return CaptureResult.Ok(compositeBitmap, captureMode: CoreGraphicsCaptureMode);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error capturing all displays.");
      return CaptureResult.Fail(ex);
    }
  }

  private CursorBitmapSnapshot? CaptureCursorSnapshot()
  {
    using var autoreleasePool = AppKit.CreateAutoreleasePool();

    var cursor = AppKit.GetCurrentCursor();
    if (cursor == nint.Zero)
    {
      _logger.LogDebug("Failed to get current cursor from NSCursor");
      return null;
    }

    var nsImage = AppKit.GetCursorImage(cursor);
    if (nsImage == nint.Zero)
    {
      _logger.LogDebug("Failed to get cursor image");
      return null;
    }

    var (hotspotX, hotspotY) = AppKit.GetCursorHotspot(cursor);
    var cgImageRef = AppKit.GetNSImageCGImage(nsImage);
    if (cgImageRef == nint.Zero)
    {
      _logger.LogDebug("Failed to convert NSImage to CGImage");
      return null;
    }

    var cursorBitmap = CoreGraphicsHelper.CGImageToSKBitmap(cgImageRef);
    if (cursorBitmap is null)
    {
      _logger.LogDebug("Failed to convert cursor CGImage to SKBitmap");
      return null;
    }

    return new CursorBitmapSnapshot(cursorBitmap, hotspotX, hotspotY);
  }

  private CaptureResult CaptureDisplayImpl(DisplayInfo display, bool captureCursor)
  {
    try
    {
      if (!uint.TryParse(display.DeviceName, out var displayId))
      {
        return CaptureResult.Fail($"Invalid display ID: {display.DeviceName}");
      }

      // Capture the entire display
      var cgImageRef = CoreGraphics.CGDisplayCreateImage(displayId);

      if (cgImageRef == nint.Zero)
      {
        return CaptureResult.Fail("Failed to create display image.");
      }

      using var cgImageDisposer = new CallbackDisposable(
        () => CoreGraphicsHelper.ReleaseCGImage(cgImageRef));

      var bitmap = CoreGraphicsHelper.CGImageToSKBitmap(cgImageRef);

      if (bitmap is null)
      {
        return CaptureResult.Fail("Failed to convert CGImage to SKBitmap.");
      }

      if (captureCursor)
      {
        var bounds = CoreGraphics.CGDisplayBounds(displayId);
        var boundsRect = new Rectangle(
          (int)(bounds.X * display.CapturePixelsPerLayoutUnit),
          (int)(bounds.Y * display.CapturePixelsPerLayoutUnit),
          bitmap.Width,
          bitmap.Height);
        DrawCursorOnBitmap(bitmap, boundsRect, [display]);
      }

      return CaptureResult.Ok(bitmap, captureMode: CoreGraphicsCaptureMode);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error capturing display {DisplayId}.", display.DeviceName);
      return CaptureResult.Fail(ex);
    }
  }

  private void DrawCursorOnBitmap(SKBitmap bitmap, Rectangle captureArea, IReadOnlyList<DisplayInfo> displays)
  {
    try
    {
      if (!CoreGraphics.CGCursorIsVisible())
      {
        return;
      }

      if (!TryGetMouseLocationInPixelCoordinates(displays, out var cursorX, out var cursorY))
      {
        _logger.LogDebug("Failed to get mouse location");
        return;
      }

      // Adjust coordinates if we have a capture area (for multi-display)
      if (!captureArea.IsEmpty)
      {
        cursorX -= captureArea.X;
        cursorY -= captureArea.Y;
      }

      using var cursorSnapshot = _uiThread.Invoke(CaptureCursorSnapshot);
      if (cursorSnapshot is null)
      {
        return;
      }

      // Calculate draw position (cursor position minus hotspot)
      var drawX = cursorX - (int)cursorSnapshot.HotspotX;
      var drawY = cursorY - (int)cursorSnapshot.HotspotY;

      // Draw cursor on bitmap
      using var canvas = new SKCanvas(bitmap);
      canvas.DrawBitmap(cursorSnapshot.Bitmap, drawX, drawY);
    }
    catch (Exception ex)
    {
      _logger.LogDebug(ex, "Failed to draw cursor on bitmap");
    }
  }

  private bool TryGetMouseLocationInPixelCoordinates(
    IReadOnlyList<DisplayInfo> displays,
    out int x,
    out int y)
  {
    x = 0;
    y = 0;

    var cgEventRef = CoreGraphics.CGEventCreate(nint.Zero);
    if (cgEventRef == nint.Zero)
    {
      return false;
    }

    using var cgEventDisposer = new CallbackDisposable(
      () => CoreGraphics.CFRelease(cgEventRef));

    var location = CoreGraphics.CGEventGetLocation(cgEventRef);

    var unscaledX = (int)Math.Round(location.X);
    var unscaledY = (int)Math.Round(location.Y);

    if (displays.Any(d => d.LayoutBounds.Contains(unscaledX, unscaledY)))
    {
      x = unscaledX;
      y = unscaledY;
      return true;
    }

    return false;
  }

  private sealed class CursorBitmapSnapshot(SKBitmap bitmap, double hotspotX, double hotspotY) : IDisposable
  {
    public SKBitmap Bitmap { get; } = bitmap;

    public double HotspotX { get; } = hotspotX;

    public double HotspotY { get; } = hotspotY;

    public void Dispose()
    {
      Bitmap.Dispose();
    }
  }
}
