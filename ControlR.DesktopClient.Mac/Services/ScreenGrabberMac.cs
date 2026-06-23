using ControlR.DesktopClient.Common.Models;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.DesktopClient.Mac.Helpers;
using ControlR.Libraries.Avalonia.Services;
using ControlR.Libraries.NativeInterop.Mac;
using ControlR.Libraries.Shared.Primitives;
using Microsoft.Extensions.Logging;
using SkiaSharp;
using System.Drawing;

namespace ControlR.DesktopClient.Mac.Services;

/// <summary>
/// Captures screenshots and cursor images on macOS using CoreGraphics.
/// </summary>
public sealed class ScreenGrabberMac(
  IDisplayManager displayManager,
  IUiDispatcher dispatcher,
  ILogger<ScreenGrabberMac> logger) : IScreenGrabber
{
  private const string CoreGraphicsCaptureMode = "CoreGraphics";

  private readonly IUiDispatcher _dispatcher = dispatcher;
  private readonly IDisplayManager _displayManager = displayManager;
  private readonly ILogger<ScreenGrabberMac> _logger = logger;

  /// <summary>
  /// Captures all displays as a single composite image.
  /// </summary>
  /// <param name="captureCursor">Whether to capture and overlay the cursor image.</param>
  /// <returns>A capture result containing the composite bitmap or an error.</returns>
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

  /// <summary>
  /// Captures a single display.
  /// </summary>
  /// <param name="targetDisplay">The display to capture.</param>
  /// <param name="captureCursor">Whether to capture and overlay the cursor image.</param>
  /// <param name="forceKeyFrame">Ignored on macOS; present for interface compatibility.</param>
  /// <returns>A capture result containing the display bitmap or an error.</returns>
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

  /// <summary>
  /// Captures all displays and composites them into a single bitmap.
  /// Handles single-display and multi-display scenarios with cursor overlay.
  /// </summary>
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

        var cgImageRef = CoreGraphicsInterop.CGDisplayCreateImage(displayId);
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
            var display = displays[0];
            var captureArea = new Rectangle(
              (int)(display.LayoutBounds.X * display.CapturePixelsPerLayoutUnit),
              (int)(display.LayoutBounds.Y * display.CapturePixelsPerLayoutUnit),
              bitmap.Width,
              bitmap.Height);
            DrawCursorOnBitmap(bitmap, captureArea, displays, isPixelSpace: true);
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

        var cgImageRef = CoreGraphicsInterop.CGDisplayCreateImage(displayId);
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
        DrawCursorOnBitmap(compositeBitmap, boundsRect, displays, isPixelSpace: false);
      }

      return CaptureResult.Ok(compositeBitmap, captureMode: CoreGraphicsCaptureMode);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error capturing all displays.");
      return CaptureResult.Fail(ex);
    }
  }

  /// <summary>
  /// Captures the current cursor image and hotspot offset using NSCursor.
  /// </summary>
  /// <returns>A snapshot containing the cursor bitmap and hotspot, or null if capture fails.</returns>
  private CursorBitmapSnapshot? CaptureCursorSnapshot()
  {
    using var autoreleasePool = AppKitInterop.CreateAutoreleasePool();

    var cursor = AppKitInterop.GetCurrentCursor();
    if (cursor == nint.Zero)
    {
      _logger.LogDebug("Failed to get current cursor from NSCursor");
      return null;
    }

    var nsImage = AppKitInterop.GetCursorImage(cursor);
    if (nsImage == nint.Zero)
    {
      _logger.LogDebug("Failed to get cursor image");
      return null;
    }

    var (hotspotX, hotspotY) = AppKitInterop.GetCursorHotspot(cursor);
    var cgImageRef = AppKitInterop.GetNSImageCGImage(nsImage);
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

  /// <summary>
  /// Captures a single display using CGDisplayCreateImage.
  /// </summary>
  private CaptureResult CaptureDisplayImpl(DisplayInfo display, bool captureCursor)
  {
    try
    {
      if (!uint.TryParse(display.DeviceName, out var displayId))
      {
        return CaptureResult.Fail($"Invalid display ID: {display.DeviceName}");
      }

      // Capture the entire display
      var cgImageRef = CoreGraphicsInterop.CGDisplayCreateImage(displayId);

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
        var bounds = CoreGraphicsInterop.CGDisplayBounds(displayId);
        var boundsRect = new Rectangle(
          (int)(bounds.X * display.CapturePixelsPerLayoutUnit),
          (int)(bounds.Y * display.CapturePixelsPerLayoutUnit),
          bitmap.Width,
          bitmap.Height);
        DrawCursorOnBitmap(bitmap, boundsRect, [display], isPixelSpace: true);
      }

      return CaptureResult.Ok(bitmap, captureMode: CoreGraphicsCaptureMode);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error capturing display {DisplayId}.", display.DeviceName);
      return CaptureResult.Fail(ex);
    }
  }

  /// <summary>
  /// Draws the cursor onto a bitmap at the current mouse position.
  /// </summary>
  /// <param name="bitmap">The bitmap to draw onto.</param>
  /// <param name="captureArea">The capture area bounds for coordinate translation.</param>
  /// <param name="displays">The list of displays for coordinate conversion.</param>
  /// <param name="isPixelSpace">Whether coordinates are in pixel space versus logical space.</param>
  private void DrawCursorOnBitmap(
    SKBitmap bitmap,
    Rectangle captureArea,
    IReadOnlyList<DisplayInfo> displays,
    bool isPixelSpace)
  {
    try
    {
      if (!CoreGraphicsInterop.CGCursorIsVisible())
      {
        return;
      }

      int cursorX, cursorY;
      if (isPixelSpace)
      {
        if (!TryGetMouseLocationInPixelCoordinates(displays, out cursorX, out cursorY))
        {
          _logger.LogDebug("Failed to get mouse location");
          return;
        }
      }
      else
      {
        if (!TryGetMouseLocationInLogicalCoordinates(displays, out cursorX, out cursorY))
        {
          _logger.LogDebug("Failed to get mouse location");
          return;
        }
      }

      // Adjust coordinates if we have a capture area (for multi-display)
      if (!captureArea.IsEmpty)
      {
        cursorX -= captureArea.X;
        cursorY -= captureArea.Y;
      }

      using var cursorSnapshot = _dispatcher.Invoke(CaptureCursorSnapshot);
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

  /// <summary>
  /// Gets the mouse location in logical coordinates (accounting for Retina scaling).
  /// </summary>
  private bool TryGetMouseLocationInLogicalCoordinates(
    IReadOnlyList<DisplayInfo> displays,
    out int x,
    out int y)
  {
    x = 0;
    y = 0;

    var cgEventRef = CoreGraphicsInterop.CGEventCreate(nint.Zero);
    if (cgEventRef == nint.Zero)
    {
      return false;
    }

    using var cgEventDisposer = new CallbackDisposable(
      () => CoreGraphicsInterop.CFRelease(cgEventRef));

    var location = CoreGraphicsInterop.CGEventGetLocation(cgEventRef);

    var targetDisplay = displays.FirstOrDefault(d =>
      location.X >= d.LayoutBounds.Left &&
      location.X < d.LayoutBounds.Right &&
      location.Y >= d.LayoutBounds.Top &&
      location.Y < d.LayoutBounds.Bottom);
    if (targetDisplay is null)
    {
      return false;
    }

    x = (int)Math.Round(location.X);
    y = (int)Math.Round(location.Y);
    return true;
  }

  /// <summary>
  /// Gets the mouse location in pixel coordinates (native display resolution).
  /// </summary>
  private bool TryGetMouseLocationInPixelCoordinates(
    IReadOnlyList<DisplayInfo> displays,
    out int x,
    out int y)
  {
    x = 0;
    y = 0;

    var cgEventRef = CoreGraphicsInterop.CGEventCreate(nint.Zero);
    if (cgEventRef == nint.Zero)
    {
      return false;
    }

    using var cgEventDisposer = new CallbackDisposable(
      () => CoreGraphicsInterop.CFRelease(cgEventRef));

    var location = CoreGraphicsInterop.CGEventGetLocation(cgEventRef);

    var targetDisplay = displays.FirstOrDefault(d =>
      location.X >= d.LayoutBounds.Left &&
      location.X < d.LayoutBounds.Right &&
      location.Y >= d.LayoutBounds.Top &&
      location.Y < d.LayoutBounds.Bottom);
    if (targetDisplay is null)
    {
      return false;
    }

    x = (int)Math.Round(location.X * targetDisplay.CapturePixelsPerLayoutUnit);
    y = (int)Math.Round(location.Y * targetDisplay.CapturePixelsPerLayoutUnit);
    return true;
  }

  /// <summary>
  /// Holds a captured cursor bitmap with its hotspot offset.
  /// </summary>
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
