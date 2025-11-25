using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ControlR.DesktopClient.Common.Models;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.DesktopClient.Windows.Extensions;
using ControlR.Libraries.NativeInterop.Windows;
using ControlR.Libraries.Shared.Helpers;
using ControlR.Libraries.Shared.Primitives;
using Microsoft.Extensions.Logging;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Direct3D11;
using Windows.Win32.Graphics.Dxgi;
using Windows.Win32.Graphics.Dxgi.Common;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;
using ControlR.DesktopClient.Windows.Helpers;

namespace ControlR.DesktopClient.Windows.Services;

internal sealed class ScreenGrabberWindows(
  IDxOutputDuplicator dxOutputGenerator,
  IWin32Interop win32Interop,
  IDisplayManager displayManager,
  ILogger<ScreenGrabberWindows> logger) : IScreenGrabber
{
  private readonly IDisplayManager _displayManager = displayManager;
  private readonly IDxOutputDuplicator _dxOutputGenerator = dxOutputGenerator;
  private readonly ILogger<ScreenGrabberWindows> _logger = logger;
  private readonly IWin32Interop _win32Interop = win32Interop;

  private bool _inputDesktopSwitchResult = true;

  public CaptureResult CaptureAllDisplays(bool captureCursor = true)
  {
    SwitchToInputDesktop();
    return GetBitBltCapture(_displayManager.GetVirtualScreenBounds(), captureCursor);
  }

  public CaptureResult CaptureDisplay(
    DisplayInfo targetDisplay,
    bool captureCursor = true,
    bool forceKeyFrame = false)
  {
    try
    {
      SwitchToInputDesktop();

      var dxResult = GetDirectXCapture(targetDisplay, captureCursor);

      if (dxResult.IsSuccess || (dxResult.HadNoChanges && !forceKeyFrame))
      {
        return dxResult;
      }

      return GetBitBltCapture(targetDisplay.MonitorArea, captureCursor);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error grabbing screen.");
      return CaptureResult.Fail(ex);
    }
  }

  public Task Initialize(CancellationToken cancellationToken)
  {
    return Task.CompletedTask;
  }

  public Task Uninitialize(CancellationToken cancellationToken)
  {
    _dxOutputGenerator.Uninitialize();
    return Task.CompletedTask;
  }

  private CaptureResult GetBitBltCapture(
    Rectangle captureArea,
    bool captureCursor)
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
          return CaptureResult.Fail("BitBlt function failed.");
        }
      }

      if (captureCursor)
      {
        _ = TryDrawCursor(graphics, captureArea);
      }

      var skBitmap = bitmap.ToSkBitmap();
      return CaptureResult.Ok(skBitmap, isUsingGpu: false);
    }
    catch (Exception ex)
    {
      _logger.LogError(
        ex,
        "Error getting capture with BitBlt. Capture Area: {@CaptureArea}",
        captureArea);
      return CaptureResult.Fail(exception: ex);
    }
  }

  private CaptureResult GetDirectXCapture(DisplayInfo display, bool captureCursor)
  {
    var dxOutput = _dxOutputGenerator.DuplicateOutput(display.DeviceName);

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

      TryHelper.TryAll(outputDuplication.ReleaseFrame);
      outputDuplication.AcquireNextFrame(0, out var duplicateFrameInfo, out var screenResource);
      if (duplicateFrameInfo.AccumulatedFrames == 0)
      {
        return CaptureResult.NoChanges(isUsingGpu: true);
      }

      using var bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
      var bitmapData = bitmap.LockBits(bounds, ImageLockMode.WriteOnly, bitmap.PixelFormat);
      var bitmapDataPointer = bitmapData.Scan0;

      unsafe
      {
        var textureDescription = DxTextureHelper.Create2dTextureDescription(bounds.Width, bounds.Height);
        
        device.CreateTexture2D(textureDescription, null, out var texture2d);

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

      var dirtyRects = GetDirtyRects(outputDuplication);

      if (!captureCursor)
      {
        return CaptureResult.Ok(bitmap.ToSkBitmap(), true, dirtyRects);
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

      return CaptureResult.Ok(bitmap.ToSkBitmap(), true, dirtyRects);
    }
    catch (COMException ex) when (ex.Message.StartsWith("The timeout value has elapsed"))
    {
      return CaptureResult.NoChanges(isUsingGpu: true);
    }
    catch (COMException ex)
    {
      _dxOutputGenerator.SetCurrentOutputFaulted();
      _logger.LogWarning(ex, "Failed to capture with DirectX, falling back to BitBlt. Display: {DisplayId}", display.DeviceName);
      return CaptureResult.Fail(ex);
    }
    catch (Exception ex)
    {
      _dxOutputGenerator.SetCurrentOutputFaulted();
      _logger.LogError(ex, "Failed to capture with DirectX, falling back to BitBlt. Display: {DisplayId}", display.DeviceName);
      return CaptureResult.Fail(ex);
    }
  }

  private unsafe Rectangle[] GetDirtyRects(IDXGIOutputDuplication outputDuplication)
  {
    var rectSize = (uint)sizeof(RECT);
    uint bufferSizeNeeded = 0;

    try
    {
      outputDuplication.GetFrameDirtyRects(out _, out bufferSizeNeeded);
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
    var dirtyRectsPtr = (RECT*)NativeMemory.Alloc(bufferSizeNeeded);

    try
    {
      outputDuplication.GetFrameDirtyRects(bufferSizeNeeded, dirtyRectsPtr, out _);

      for (var i = 0; i < numRects; i++)
      {
        dirtyRects[i] = dirtyRectsPtr[i].ToRectangle();
      }
    }
    finally
    {
      NativeMemory.Free(dirtyRectsPtr);
    }

    return dirtyRects;
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
      }

      var virtualScreen = _displayManager.GetVirtualScreenBounds();
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