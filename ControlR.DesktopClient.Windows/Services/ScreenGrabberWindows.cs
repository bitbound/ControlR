using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ControlR.DesktopClient.Common.Models;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.DesktopClient.Common.Services;
using ControlR.DesktopClient.Windows.Extensions;
using ControlR.DesktopClient.Windows.Helpers;
using ControlR.DesktopClient.Windows.Models;
using ControlR.Libraries.NativeInterop.Windows;
using ControlR.Libraries.Shared.Primitives;
using Microsoft.Extensions.Logging;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Direct3D11;
using Windows.Win32.Graphics.Dxgi;
using Windows.Win32.Graphics.Dxgi.Common;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;

namespace ControlR.DesktopClient.Windows.Services;

internal sealed class ScreenGrabberWindows(
  TimeProvider timeProvider,
  IImageUtility bitmapUtility,
  IDxOutputGenerator dxOutputGenerator,
  IWin32Interop win32Interop,
  ILogger<ScreenGrabberWindows> logger) : IScreenGrabber
{
  private readonly IImageUtility _bitmapUtility = bitmapUtility;
  private readonly IDxOutputGenerator _dxOutputGenerator = dxOutputGenerator;
  private readonly ILogger<ScreenGrabberWindows> _logger = logger;
  private readonly TimeProvider _timeProvider = timeProvider;
  private readonly IWin32Interop _win32Interop = win32Interop;
  private bool _inputDesktopSwitchResult = true;

  public CaptureResult Capture(
    DisplayInfo targetDisplay,
    bool captureCursor = true,
    bool tryUseGpuAcceleration = true,
    int gpuCaptureTimeout = 50,
    bool allowFallbackToCpu = true)
  {
    try
    {
      var display = GetDisplays().FirstOrDefault(x => x.DeviceName == targetDisplay.DeviceName);

      if (display is null)
      {
        return CaptureResult.Fail("Display name not found.");
      }

      SwitchToInputDesktop();

      if (!tryUseGpuAcceleration)
      {
        return GetBitBltCapture(display.MonitorArea, captureCursor);
      }

      var dxResult = GetDirectXCapture(display, captureCursor);

      if (dxResult.HadNoChanges)
      {
        return dxResult;
      }

      if (dxResult.DxTimedOut && allowFallbackToCpu)
      {
        return GetBitBltCapture(display.MonitorArea, captureCursor, dxResult);
      }

      if (!dxResult.IsSuccess || dxResult.Bitmap is null || _bitmapUtility.IsEmpty(dxResult.Bitmap))
      {
        if (!allowFallbackToCpu)
        {
          return dxResult;
        }

        return GetBitBltCapture(display.MonitorArea, captureCursor, dxResult);
      }

      return dxResult;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error grabbing screen.");
      return CaptureResult.Fail(ex);
    }
  }

  public CaptureResult Capture(bool captureCursor = true)
  {
    SwitchToInputDesktop();
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

  private unsafe CaptureResult CaptureSession0Desktop()
  {
    const int WM_USER_CAPTURE_START = 0x8001;
    const int WM_USER_CAPTURE_END = 0x8002;

    var bounds = GetVirtualScreenBounds();
    using var desktopBitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
    using var desktopGraphics = Graphics.FromImage(desktopBitmap);

    var windowInfos = _win32Interop.GetVisibleWindows();
    windowInfos.Reverse();

    var shellWindow = windowInfos.FirstOrDefault(x => x.Title == "ControlR Shell");
    if (shellWindow is not null)
    {
      var hwnd = new HWND(shellWindow.WindowHandle);
      nuint result = 0;
      PInvoke.SendMessageTimeout(hwnd, WM_USER_CAPTURE_START, 0, 0,
        SEND_MESSAGE_TIMEOUT_FLAGS.SMTO_NORMAL, 100, &result);
    }

    try
    {
      foreach (var window in windowInfos)
      {
        try
        {
          var hwnd = new HWND(window.WindowHandle);

          var windowDc = PInvoke.GetWindowDC(hwnd);
          using var windowBitmap = new Bitmap(window.Width, window.Height, PixelFormat.Format32bppArgb);

          try
          {
            using var windowGraphics = Graphics.FromImage(windowBitmap);
            using var disposableHdc = windowGraphics.GetDisposableHdc();
            var targetHdc = new HDC(disposableHdc.Value);

            var success = PInvoke.PrintWindow(hwnd, targetHdc, 0);

            if (!success)
            {
              _logger.LogDebug("Failed to capture window {WindowHandle}.", window.WindowHandle);
              return CaptureResult.Fail("Failed to print window.");
            }
          }
          finally
          {
            _ = PInvoke.ReleaseDC(hwnd, windowDc);
          }

          desktopGraphics.DrawImage(windowBitmap, window.X, window.Y);
        }
        catch (Exception ex)
        {
          _logger.LogDebug(ex, "Failed to capture window {WindowHandle}", window.WindowHandle);
          return CaptureResult.Fail(ex, $"Failed to capture window {window.WindowHandle}.");
        }
      }

      return CaptureResult.Ok(desktopBitmap.ToSKBitmap(), false);
    }
    finally
    {
      if (shellWindow is not null)
      {
        var hwnd = new HWND(shellWindow.WindowHandle);
        nuint result = 0;
        PInvoke.SendMessageTimeout(hwnd, WM_USER_CAPTURE_END, 0, 0,
          SEND_MESSAGE_TIMEOUT_FLAGS.SMTO_NORMAL, 500, &result);
      }
    }
  }
  private CaptureResult GetBitBltCapture(
    Rectangle captureArea,
    bool captureCursor,
    CaptureResult? dxResult = null)
  {
    try
    {
      var screenDc = PInvoke.GetDC(HWND.Null);
      using var callback = new CallbackDisposable(() => _ = PInvoke.ReleaseDC(HWND.Null, screenDc));

      using var bitmap = new Bitmap(captureArea.Width, captureArea.Height);
      using var graphics = Graphics.FromImage(bitmap);

      using (var targetDc = graphics.GetDisposableHdc())
      {
        var bitBltResult = PInvoke.BitBlt(new HDC(targetDc.Value), 0, 0, captureArea.Width, captureArea.Height,
          screenDc, captureArea.X, captureArea.Y, ROP_CODE.SRCCOPY);

        if (!bitBltResult)
        {
          return CaptureResult.Fail("BitBlt function failed.", dxResult);
        }
      }

      if (captureCursor)
      {
        _ = TryDrawCursor(graphics, captureArea);
      }

      return CaptureResult.Ok(bitmap.ToSKBitmap(), isUsingGpu: false);
    }
    catch (Exception ex)
    {
      _logger.LogError(
        ex,
        "Error getting capture with BitBlt. Capture Area: {@CaptureArea}",
        captureArea);
      return CaptureResult.Fail(exception: ex, dxCaptureResult: dxResult);
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

      using var bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
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
        return CaptureResult.Ok(bitmap.ToSKBitmap(), true, dirtyRects);
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

      return CaptureResult.Ok(bitmap.ToSKBitmap(), true, dirtyRects);
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

  private void SwitchToInputDesktop()
  {
    var inputDesktopSwitchResult = _win32Interop.SwitchToInputDesktop();
    if (!inputDesktopSwitchResult && _inputDesktopSwitchResult)
    {
      _logger.LogWarning("Failed to switch to input desktop. This may be caused by hooks in the current desktop.");
    }
    _inputDesktopSwitchResult = inputDesktopSwitchResult;
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
}