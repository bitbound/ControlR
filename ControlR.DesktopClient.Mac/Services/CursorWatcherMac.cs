using Bitbound.SimpleMessenger;
using ControlR.DesktopClient.Common.Messages;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.DesktopClient.Common.Services;
using ControlR.DesktopClient.Mac.Helpers;
using ControlR.Libraries.Hosting;
using ControlR.Libraries.NativeInterop.Mac;
using ControlR.Libraries.Api.Contracts.Enums;
using Microsoft.Extensions.Logging;
using SkiaSharp;

namespace ControlR.DesktopClient.Mac.Services;

internal class CursorWatcherMac(
  TimeProvider timeProvider,
  IDisplayManager displayManager,
  IMessenger messenger,
  IImageUtility imageUtility,
  ILogger<CursorWatcherMac> logger) 
  : PeriodicBackgroundService(TimeSpan.FromMilliseconds(10), timeProvider, logger)
{
  private readonly IDisplayManager _displayManager = displayManager;
  private readonly IImageUtility _imageUtility = imageUtility;
  private readonly IMessenger _messenger = messenger;

  private string? _lastCursorBase64;
  private nint _lastCursorPointer = nint.Zero;

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    Logger.LogInformation("Starting cursor watcher for macOS");
    await base.ExecuteAsync(stoppingToken);
  }

  protected override async Task HandleElapsed()
  {
    await CheckCursorChange();
  }

  private async Task CheckCursorChange()
  {
    var cursor = AppKit.GetCurrentCursor();
    if (cursor == nint.Zero)
    {
      return;
    }

    if (cursor == _lastCursorPointer)
    {
      return;
    }

    _lastCursorPointer = cursor;

    var nsImage = AppKit.GetCursorImage(cursor);
    if (nsImage == nint.Zero)
    {
      Logger.LogDebug("Failed to get cursor image");
      return;
    }

    var cgImageRef = AppKit.GetNSImageCGImage(nsImage);
    if (cgImageRef == nint.Zero)
    {
      Logger.LogDebug("Failed to convert NSImage to CGImage");
      return;
    }

    var (hotspotX, hotspotY) = AppKit.GetCursorHotspot(cursor);

    var scaleFactor = await TryGetCurrentCursorDisplayScaleFactor();

    var cursorBase64 = ConvertCursorToPngBase64(cgImageRef, scaleFactor);
    if (cursorBase64 is null)
    {
      return;
    }

    if (cursorBase64 == _lastCursorBase64)
    {
      return;
    }

    _lastCursorBase64 = cursorBase64;

    var changeMessage = new CursorChangedMessage(
      PointerCursor.Custom,
      cursorBase64,
      (ushort)Math.Clamp((int)Math.Round(hotspotX), 0, ushort.MaxValue),
      (ushort)Math.Clamp((int)Math.Round(hotspotY), 0, ushort.MaxValue));

    await _messenger.Send(changeMessage);

    Logger.LogDebug("Cursor changed. Broadcasted new cursor image.");
  }

  private string? ConvertCursorToPngBase64(nint cgImageRef, double scaleFactor)
  {
    try
    {
      using var cursorBitmap = CoreGraphicsHelper.CGImageToSKBitmap(cgImageRef);
      if (cursorBitmap is null)
      {
        Logger.LogDebug("Failed to convert cursor CGImage to SKBitmap");
        return null;
      }

      if (cursorBitmap.Width <= 0 || cursorBitmap.Height <= 0)
      {
        Logger.LogDebug(
          "Invalid cursor dimensions: {Width}x{Height}",
          cursorBitmap.Width,
          cursorBitmap.Height);
        return null;
      }

      // NSCursor images can be provided at backing pixel resolution (e.g., 2x on Retina).
      // CSS cursor rendering in browsers uses logical (CSS pixel) sizing; sending a 2x
      // cursor image will render oversized and cause hotspot misalignment.
      // Normalize the cursor bitmap to logical pixels by dividing by the current display scale.
      using var normalizedBitmap = NormalizeCursorBitmap(cursorBitmap, scaleFactor);
      var bitmapToEncode = normalizedBitmap ?? cursorBitmap;

      var pngBytes = _imageUtility.Encode(bitmapToEncode, SKEncodedImageFormat.Png);

      return Convert.ToBase64String(pngBytes);
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Failed to convert cursor to PNG base64");
      return null;
    }
  }

  private SKBitmap? NormalizeCursorBitmap(SKBitmap source, double scaleFactor)
  {
    if (scaleFactor <= 1.01)
    {
      return null;
    }

    var targetWidth = (int)Math.Max(1, Math.Round(source.Width / scaleFactor));
    var targetHeight = (int)Math.Max(1, Math.Round(source.Height / scaleFactor));

    // Avoid resizing when the computed dimensions match.
    if (targetWidth == source.Width && targetHeight == source.Height)
    {
      return null;
    }

    var info = new SKImageInfo(targetWidth, targetHeight, source.ColorType, source.AlphaType);
    var resized = source.Resize(info, SKSamplingOptions.Default);

    if (resized is null)
    {
      Logger.LogDebug(
        "Failed to resize cursor bitmap from {SourceWidth}x{SourceHeight} to {TargetWidth}x{TargetHeight}",
        source.Width,
        source.Height,
        targetWidth,
        targetHeight);
    }

    return resized;
  }

  private async Task<double> TryGetCurrentCursorDisplayScaleFactor()
  {
    try
    {
      var displays = await _displayManager.GetDisplays();
      if (displays.Count == 0)
      {
        return 1.0;
      }

      nint cgEventRef = nint.Zero;
      try
      {
        cgEventRef = CoreGraphics.CGEventCreate(nint.Zero);
        if (cgEventRef == nint.Zero)
        {
          return 1.0;
        }

        var location = CoreGraphics.CGEventGetLocation(cgEventRef);

        var displayIds = new uint[1];
        var rect = new CoreGraphics.CGRect(location.X, location.Y, 1, 1);
        var result = CoreGraphics.CGGetDisplaysWithRect(rect, 1, displayIds, out var matchingDisplayCount);
        if (result == 0 && matchingDisplayCount > 0)
        {
          var displayIdString = displayIds[0].ToString();
          var display = displays.FirstOrDefault(d => d.DeviceName == displayIdString);
          if (display is not null && display.ScaleFactor > 0)
          {
            return display.ScaleFactor;
          }
        }
      }
      finally
      {
        if (cgEventRef != nint.Zero)
        {
          CoreGraphics.CFRelease(cgEventRef);
        }
      }
    }
    catch (Exception ex)
    {
      Logger.LogDebug(ex, "Error determining current cursor display scale factor");
    }

    return 1.0;
  }
}
