using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Direct3D11;
using Windows.Win32.Graphics.Dxgi;
using Windows.Win32.Graphics.Dxgi.Common;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;
using ControlR.Streamer.Extensions;
using ControlR.Streamer.Helpers;
using ControlR.Streamer.Models;

namespace ControlR.Streamer.Services;

public interface IScreenGrabber
{
  /// <summary>
  ///   Gets a capture of a specific display.
  /// </summary>
  /// <param name="targetDisplay">The display to capture.  Retrieve current displays from <see cref="GetDisplays" />. </param>
  /// <param name="captureCursor">Whether to include the cursor in the capture.</param>
  /// <param name="tryUseDirectX">Whether to attempt using DirectX (DXGI) for getting the capture.</param>
  /// <param name="directXTimeout">
  ///   The amount of time, in milliseconds, to allow DirectX to attempt to capture the screen.
  ///   If no screen changes have occurred within this time, the capture will time out.
  /// </param>
  /// <param name="allowFallbackToBitBlt">
  ///   Whether to allow fallback to BitBlt for capture, which is not DirectX-accelerated, in the event of timeout or
  ///   exception.
  /// </param>
  /// <returns>
  ///   A result object indicating whether the capture was successful.
  ///   If successful, the result will contain the <see cref="Bitmap" /> of the capture.
  /// </returns>
  CaptureResult Capture(
    DisplayInfo targetDisplay,
    bool captureCursor = true,
    bool tryUseDirectX = true,
    int directXTimeout = 50,
    bool allowFallbackToBitBlt = true);

  /// <summary>
  ///   Gets a capture of all displays.  This method is not DirectX-accelerated.
  /// </summary>
  /// <param name="captureCursor">Whether to include the cursor in the capture.</param>
  /// <returns>
  ///   A result object indicating whether the capture was successful.
  ///   If successful, the result will contain the <see cref="Bitmap" /> of the capture.
  /// </returns>
  CaptureResult Capture(bool captureCursor = true);

  /// <summary>
  ///   Return info about the connected displays.
  /// </summary>
  /// <returns></returns>
  IEnumerable<DisplayInfo> GetDisplays();

  /// <summary>
  ///   Returns the area encompassing all displays.
  /// </summary>
  Rectangle GetVirtualScreenBounds();
}

internal sealed class ScreenGrabber(
  TimeProvider timeProvider,
  IBitmapUtility bitmapUtility,
  IDxOutputGenerator dxOutputGenerator,
  IWin32Interop win32Interop,
  ILogger<ScreenGrabber> logger) : IScreenGrabber
{
  private readonly TimeProvider _timeProvider = timeProvider;
  private readonly IBitmapUtility _bitmapUtility = bitmapUtility;
  private readonly IWin32Interop _win32Interop = win32Interop;
  private readonly IDxOutputGenerator _dxOutputGenerator = dxOutputGenerator;
  private readonly ILogger<ScreenGrabber> _logger = logger;

  public CaptureResult Capture(
    DisplayInfo targetDisplay,
    bool captureCursor = true,
    bool tryUseDirectX = true,
    int directXTimeout = 50,
    bool allowFallbackToBitBlt = true)
  {
    try
    {
      var display = GetDisplays().FirstOrDefault(x => x.DeviceName == targetDisplay.DeviceName);

      if (display is null)
      {
        return CaptureResult.Fail("Display name not found.");
      }

      if (!tryUseDirectX)
      {
        return GetBitBltCapture(display.MonitorArea, captureCursor);
      }

      var result = GetDirectXCapture(display, captureCursor);

      if (result.HadNoChanges)
      {
        return result;
      }

      if (result.DxTimedOut && allowFallbackToBitBlt)
      {
        return GetBitBltCapture(display.MonitorArea, captureCursor);
      }

      if (!result.IsSuccess || result.Bitmap is null || _bitmapUtility.IsEmpty(result.Bitmap))
      {
        if (!allowFallbackToBitBlt)
        {
          return result;
        }

        return GetBitBltCapture(display.MonitorArea, captureCursor);
      }

      return result;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error grabbing screen.");
      return CaptureResult.Fail(ex);
    }
  }

  public CaptureResult Capture(bool captureCursor = true)
  {
    return GetBitBltCapture(GetVirtualScreenBounds(), captureCursor);
  }

  public IEnumerable<DisplayInfo> GetDisplays()
  {
    return DisplaysEnumerationHelper.GetDisplays();
  }

  public Rectangle GetVirtualScreenBounds()
  {
    var width = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CXVIRTUALSCREEN);
    var height = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CYVIRTUALSCREEN);
    var left = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_XVIRTUALSCREEN);
    var top = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_YVIRTUALSCREEN);
    return new Rectangle(left, top, width, height);
  }

  private unsafe Rectangle[] GetDirtyRects(IDXGIOutputDuplication outputDuplication)
  {
    var rectSize = (uint)sizeof(RECT);
    uint bufferSizeNeeded = 0;

    try
    {
      outputDuplication.GetFrameDirtyRects(0, out _, out bufferSizeNeeded);
    }
    catch
    {
      // This can throw transiently.  Ignore.  Buffer size will be 0.
    }

    if (bufferSizeNeeded == 0)
    {
      return [];
    }

    var numRects = (int)(bufferSizeNeeded / rectSize);
    var dirtyRects = new Rectangle[numRects];

    var dirtyRectsPtr = stackalloc RECT[numRects];
    outputDuplication.GetFrameDirtyRects(bufferSizeNeeded, dirtyRectsPtr, out _);

    for (var i = 0; i < numRects; i++)
    {
      dirtyRects[i] = dirtyRectsPtr[i].ToRectangle();
    }

    return dirtyRects;
  }

  private bool IsDxOutputHealthy(DxOutput dxOutput)
  {
    return _timeProvider.GetLocalNow() - dxOutput.LastSuccessfulCapture < TimeSpan.FromSeconds(1.5);
  }

  private unsafe Rectangle TryDrawCursor(Graphics graphics, Rectangle captureArea)
  {
    try
    {
      // Get cursor information to draw on the screenshot.
      var ci = new CURSORINFO();
      ci.cbSize = (uint)Marshal.SizeOf(ci);
      PInvoke.GetCursorInfo(ref ci);

      if (!ci.flags.HasFlag(CURSORINFO_FLAGS.CURSOR_SHOWING))
      {
        return Rectangle.Empty;
      }

      using var icon = Icon.FromHandle(ci.hCursor);

      uint hotspotX = 0;
      uint hotspotY = 0;
      var hicon = new HICON(icon.Handle);
      var iconInfoPtr = stackalloc ICONINFO[1];
      if (PInvoke.GetIconInfo(hicon, iconInfoPtr))
      {
        hotspotX = iconInfoPtr->xHotspot;
        hotspotY = iconInfoPtr->yHotspot;
        PInvoke.DestroyIcon(hicon);
      }

      var virtualScreen = GetVirtualScreenBounds();
      var x = (int)(ci.ptScreenPos.X - virtualScreen.Left - captureArea.Left - hotspotX);
      var y = (int)(ci.ptScreenPos.Y - virtualScreen.Top - captureArea.Top - hotspotY);

      var targetArea = new Rectangle(x, y, icon.Width, icon.Height);
      if (!captureArea.Contains(targetArea))
      {
        _logger.LogDebug("Cursor is outside of capture area. Skipping.");
        return Rectangle.Empty;
      }

      graphics.DrawIcon(icon, x, y);

      return targetArea;
    }
    catch (Exception ex)
    {
      _logger.LogDebug(ex, "Error while drawing cursor.");
      return Rectangle.Empty;
    }
  }

  private CaptureResult GetBitBltCapture(Rectangle captureArea, bool captureCursor)
  {
    try
    {
      var hwnd = PInvoke.GetDesktopWindow();
      var screenDc = PInvoke.GetWindowDC(hwnd);
      using var callback = new CallbackDisposable(() => _ = PInvoke.ReleaseDC(hwnd, screenDc));

      var bitmap = new Bitmap(captureArea.Width, captureArea.Height);
      using var graphics = Graphics.FromImage(bitmap);

      using (var targetDc = graphics.GetDisposableHdc())
      {
        var bitBltResult = PInvoke.BitBlt(new HDC(targetDc.Value), 0, 0, captureArea.Width, captureArea.Height,
          screenDc, captureArea.X, captureArea.Y, ROP_CODE.SRCCOPY);

        if (!bitBltResult)
        {
          return CaptureResult.Fail("BitBlt function failed.");
        }
      }

      if (captureCursor)
      {
        _ = TryDrawCursor(graphics, captureArea);
      }

      return CaptureResult.Ok(bitmap,isUsingGpu: false);
    }
    catch (Exception ex)
    {
      _logger.LogError(
        ex, 
        "Error getting capture with BitBlt. Capture Area: {@CaptureArea}",
        captureArea);
      return CaptureResult.Fail(ex);
    }
  }

  private CaptureResult GetDirectXCapture(DisplayInfo display, bool captureCursor)
  {
    var dxOutput = _dxOutputGenerator.GetDxOutput(display.DeviceName);

    if (dxOutput is null)
    {
      return CaptureResult.Fail("DirectX output not found.");
    }

    try
    {
      var outputDuplication = dxOutput.OutputDuplication;
      var device = dxOutput.Device;
      var deviceContext = dxOutput.DeviceContext;
      var bounds = new Rectangle(0, 0, dxOutput.Bounds.Width, dxOutput.Bounds.Height);

      outputDuplication.AcquireNextFrame(50, out var duplicateFrameInfo, out var screenResource);

      if (duplicateFrameInfo.AccumulatedFrames == 0)
      {
        try
        {
          outputDuplication.ReleaseFrame();
        }
        catch
        {
          // No-op;
        }

        return IsDxOutputHealthy(dxOutput)
          ? CaptureResult.NoChanges()
          : CaptureResult.NoAccumulatedFrames();
      }

      var bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
      var bitmapData = bitmap.LockBits(bounds, ImageLockMode.WriteOnly, bitmap.PixelFormat);
      var bitmapDataPointer = bitmapData.Scan0;

      Rectangle[] dirtyRects;

      unsafe
      {
        dirtyRects = GetDirtyRects(outputDuplication);

        var textureDescription = DxTextureHelper.Create2dTextureDescription(bounds.Width, bounds.Height);
        ID3D11Texture2D_unmanaged* texture2dPtr;
        device.CreateTexture2D(textureDescription, null, &texture2dPtr);

        if (Marshal.GetObjectForIUnknown(new nint(texture2dPtr)) is not ID3D11Texture2D texture2d)
        {
          texture2dPtr->Release();
          return CaptureResult.Fail("Failed to create DirectX Texture.");
        }

        // ReSharper disable once SuspiciousTypeConversion.Global
        deviceContext.CopyResource(texture2d, (ID3D11Texture2D)screenResource);

        var subResource = new D3D11_MAPPED_SUBRESOURCE();
        var subResourceRef = &subResource;
        deviceContext.Map(texture2d, 0, D3D11_MAP.D3D11_MAP_READ, 0, subResourceRef);
        var subResPointer = new nint(subResource.pData);

        for (var y = 0; y < bounds.Height; y++)
        {
          var bitmapIndex = (void*)(bitmapDataPointer + y * bitmapData.Stride);
          var resourceIndex = (void*)(subResPointer + y * subResource.RowPitch);

          Unsafe.CopyBlock(bitmapIndex, resourceIndex, (uint)bitmapData.Stride);
        }

        bitmap.UnlockBits(bitmapData);
        deviceContext.Unmap(texture2d, 0);
        Marshal.FinalReleaseComObject(texture2d);
        texture2dPtr->Release();
        Marshal.FinalReleaseComObject(screenResource);
      }

      switch (dxOutput.Rotation)
      {
        case DXGI_MODE_ROTATION.DXGI_MODE_ROTATION_UNSPECIFIED:
        case DXGI_MODE_ROTATION.DXGI_MODE_ROTATION_IDENTITY:
          break;
        case DXGI_MODE_ROTATION.DXGI_MODE_ROTATION_ROTATE90:
          bitmap.RotateFlip(RotateFlipType.Rotate270FlipNone);
          break;
        case DXGI_MODE_ROTATION.DXGI_MODE_ROTATION_ROTATE180:
          bitmap.RotateFlip(RotateFlipType.Rotate180FlipNone);
          break;
        case DXGI_MODE_ROTATION.DXGI_MODE_ROTATION_ROTATE270:
          bitmap.RotateFlip(RotateFlipType.Rotate90FlipNone);
          break;
      }

      dxOutput.LastSuccessfulCapture = _timeProvider.GetLocalNow();

      if (!captureCursor)
      {
        return CaptureResult.Ok(bitmap, true, dirtyRects);
      }

      if (!dxOutput.LastCursorArea.IsEmpty)
      {
        dirtyRects = [.. dirtyRects, dxOutput.LastCursorArea];
      }

      using var graphics = Graphics.FromImage(bitmap);

      var iconArea = TryDrawCursor(graphics, display.MonitorArea);
      if (!iconArea.IsEmpty)
      {
        dirtyRects = [.. dirtyRects, iconArea];
        dxOutput.LastCursorArea = iconArea;
      }
      else
      {
        dxOutput.LastCursorArea = Rectangle.Empty;
      }

      return CaptureResult.Ok(bitmap, true, dirtyRects);
    }
    catch (COMException ex) when (ex.Message.StartsWith("The timeout value has elapsed"))
    {
      return IsDxOutputHealthy(dxOutput)
        ? CaptureResult.NoChanges()
        : CaptureResult.TimedOut();
    }
    catch (COMException ex)
    {
      _dxOutputGenerator.RefreshOutput();
      _logger.LogWarning(ex, "DirectX outputs need to be refreshed.");
      return CaptureResult.Fail(ex);
    }
    catch (Exception ex)
    {
      _dxOutputGenerator.RefreshOutput();
      _logger.LogError(ex, "Error while capturing with DirectX.");
      return CaptureResult.Fail(ex);
    }
    finally
    {
      try
      {
        dxOutput.OutputDuplication.ReleaseFrame();
      }
      catch
      {
        // Ignore.
      }
    }
  }
}