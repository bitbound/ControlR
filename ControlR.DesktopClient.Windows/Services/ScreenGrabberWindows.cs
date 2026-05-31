using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ControlR.DesktopClient.Common.Models;
using ControlR.DesktopClient.Common.Extensions;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.DesktopClient.Common.State;
using ControlR.DesktopClient.Windows.Extensions;
using ControlR.DesktopClient.Windows.Models;
using ControlR.Libraries.NativeInterop.Windows;
using ControlR.Libraries.Shared.Helpers;
using ControlR.Libraries.Shared.Primitives;
using Microsoft.Extensions.Logging;
using SkiaSharp;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Direct3D11;
using Windows.Win32.Graphics.Dxgi;
using Windows.Win32.Graphics.Dxgi.Common;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;
using ControlR.DesktopClient.Windows.Helpers;
using ControlR.Libraries.Shared.Logging;

namespace ControlR.DesktopClient.Windows.Services;

/// <summary>
/// Windows-specific screen grabber utilizing DirectX Desktop Duplication 
/// and GDI BitBlt as a fallback.
/// </summary>
internal sealed class ScreenGrabberWindows : IScreenGrabber
{
  private const string DirectXCaptureMode = "DirectX";
  private const string GdiCaptureMode = "GDI";

  private readonly IDisplayManager _displayManager;
  private readonly IDxOutputDuplicator _dxOutputGenerator;
  private readonly ILogger<ScreenGrabberWindows> _logger;
  private readonly IRemoteControlSessionState _sessionState;
  private readonly IWin32Interop _win32Interop;

  private IntPtr _cachedCursorHandle = IntPtr.Zero;
  private SKBitmap? _cachedCursorSkBitmap;
  private bool _inputDesktopSwitchResult = true;

  public ScreenGrabberWindows(
    IRemoteControlSessionState sessionState,
    IDxOutputDuplicator dxOutputGenerator,
    IWin32Interop win32Interop,
    IDisplayManager displayManager,
    ILogger<ScreenGrabberWindows> logger)
  {
    _sessionState = sessionState;
    _dxOutputGenerator = dxOutputGenerator;
    _win32Interop = win32Interop;
    _displayManager = displayManager;
    _logger = logger;
  }

  /// <summary>
  /// Captures the entire virtual desktop as a single composite image.
  /// </summary>
  /// <param name="captureCursor">Whether to include the system cursor in the capture.</param>
  /// <returns>A result containing the composite bitmap.</returns>
  public async Task<CaptureResult> CaptureAllDisplays(bool captureCursor = true)
  {
    SwitchToInputDesktop();
    var bounds = await _displayManager.GetVirtualScreenLayoutBounds();
    var captureArea = new Rectangle(bounds.X, bounds.Y, bounds.Width, bounds.Height);
    return GetBitBltCapture(captureArea, captureCursor);
  }

  /// <summary>
  /// Captures a specific display using DirectX if available, falling back to GDI if needed.
  /// </summary>
  /// <param name="targetDisplay">Metadata for the display to capture.</param>
  /// <param name="captureCursor">Whether to include the system cursor.</param>
  /// <param name="forceKeyFrame">Whether to force a full frame refresh.</param>
  /// <returns>The capture result containing the bitmap and any dirty regions.</returns>
  public async Task<CaptureResult> CaptureDisplay(
    DisplayInfo targetDisplay,
    bool captureCursor = true,
    bool forceKeyFrame = false)
  {
    try
    {
      SwitchToInputDesktop();

      if (_sessionState.EnableDirectX)
      {
        var dxResult = GetDirectXCapture(targetDisplay, captureCursor);
        if (dxResult.IsSuccess || (dxResult.HadNoChanges && !forceKeyFrame))
        {
          return dxResult;
        }
      }

      return GetBitBltCapture(targetDisplay.LayoutBounds, captureCursor);
    }
    catch (Exception ex)
    {
      _logger.LogErrorDeduped("Error grabbing screen.", exception: ex);
      return CaptureResult.Fail(ex);
    }
  }

  /// <summary>
  /// Releases the cursor cache and DirectX output generator.
  /// </summary>
  public ValueTask DisposeAsync()
  {
    _cachedCursorSkBitmap?.Dispose();
    _dxOutputGenerator.Dispose();
    return ValueTask.CompletedTask;
  }

  public Task Initialize(CancellationToken cancellationToken)
  {
    return Task.CompletedTask;
  }

  /// <summary>
  /// Adjusts bitmap orientation according to the specified DXGI rotation.
  /// </summary>
  private static void ApplyRotation(Bitmap bitmap, DXGI_MODE_ROTATION rotation)
  {
    switch (rotation)
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
  }

  /// <summary>
  /// Transfers frame data from a GPU texture to a CPU-accessible bitmap.
  /// </summary>
  private unsafe Bitmap CopyDxTextureToBitmap(
    ID3D11Device device,
    ID3D11DeviceContext deviceContext,
    IDXGIResource screenResource,
    Rectangle bounds)
  {
    var bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);

    try
    {
      var bitmapData = bitmap.LockBits(bounds, ImageLockMode.WriteOnly, bitmap.PixelFormat);
      using var bitmapUnlocker = new CallbackDisposable(() => bitmap.UnlockBits(bitmapData));

      var textureDescription = DxTextureHelper.Create2dTextureDescription(bounds.Width, bounds.Height);

      device.CreateTexture2D(textureDescription, null, out var texture2d);
      using var texture2dDisposer = new CallbackDisposable(() =>
      {
        Marshal.FinalReleaseComObject(texture2d);
      });

      // ReSharper disable once SuspiciousTypeConversion.Global
      deviceContext.CopyResource(texture2d, (ID3D11Texture2D)screenResource);

      var subResource = new D3D11_MAPPED_SUBRESOURCE();
      var subResourceRef = &subResource;
      deviceContext.Map(texture2d, 0, D3D11_MAP.D3D11_MAP_READ, 0, subResourceRef);
      using var mapDisposer = new CallbackDisposable(() => deviceContext.Unmap(texture2d, 0));

      var subResPointer = new nint(subResource.pData);

      for (var y = 0; y < bounds.Height; y++)
      {
        var bitmapIndex = (void*)(bitmapData.Scan0 + y * bitmapData.Stride);
        var resourceIndex = (void*)(subResPointer + y * subResource.RowPitch);

        Unsafe.CopyBlock(bitmapIndex, resourceIndex, (uint)bitmapData.Stride);
      }

      return bitmap;
    }
    catch
    {
      bitmap.Dispose();
      throw;
    }
  }

  /// <summary>
  /// Renders the cursor and tracks the affected screen areas for dirty rect calculation.
  /// </summary>
  private Rectangle[] DrawCursorAndGetDirtyRects(
    SKCanvas canvas,
    DxOutput dxOutput,
    Rectangle captureArea,
    CURSORINFO? cursorInfo)
  {
    var dirtyRects = new List<Rectangle>();

    if (!dxOutput.LastCursorArea.IsEmpty)
    {
      dirtyRects.Add(dxOutput.LastCursorArea);
    }

    var iconArea = TryDrawCursor(canvas, captureArea, cursorInfo);
    if (!iconArea.IsEmpty)
    {
      dirtyRects.Add(iconArea);
      dxOutput.LastCursorArea = iconArea;
    }
    else
    {
      dxOutput.LastCursorArea = Rectangle.Empty;
    }

    return [.. dirtyRects];
  }

  /// <summary>
  /// Performs a legacy screen capture using GDI BitBlt.
  /// </summary>
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

      var skBitmap = bitmap.ToSkBitmap();
      if (captureCursor)
      {
        var cursorInfo = TryGetCursorInfo();
        using var canvas = new SKCanvas(skBitmap);
        _ = TryDrawCursor(canvas, captureArea, cursorInfo);
      }

      return CaptureResult.Ok(skBitmap, captureMode: GdiCaptureMode);
    }
    catch (Exception ex)
    {
      _logger.LogErrorDeduped(
        "Error getting capture with BitBlt. Capture Area: {@CaptureArea}",
        exception: ex,
        args: captureArea);
      return CaptureResult.Fail(exception: ex);
    }
  }

  /// <summary>
  /// Attempts a high-performance capture using the DXGI Desktop Duplication API.
  /// </summary>
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
      var bounds = new Rectangle(0, 0, dxOutput.Bounds.Width, dxOutput.Bounds.Height);

      TryHelper.TryAll(outputDuplication.ReleaseFrame);
      outputDuplication.AcquireNextFrame(0, out var duplicateFrameInfo, out var screenResource);
      using var screenResourceDisposer = new CallbackDisposable(() => Marshal.FinalReleaseComObject(screenResource));

      if (duplicateFrameInfo.AccumulatedFrames == 0)
      {
        return HandleCursorOnlyUpdate(dxOutput, display.LayoutBounds, captureCursor);
      }

      using var bitmap = CopyDxTextureToBitmap(dxOutput.Device, dxOutput.DeviceContext, screenResource, bounds);
      ApplyRotation(bitmap, dxOutput.Rotation);

      var dirtyRects = GetDirtyRects(outputDuplication);
      var cursorInfo = TryGetCursorInfo();
      var cursorVisible = cursorInfo?.flags.HasFlag(CURSORINFO_FLAGS.CURSOR_SHOWING) ?? false;
      var cursorHandle = cursorInfo?.hCursor ?? IntPtr.Zero;

      // Convert to SKBitmap once, then cache/modify as needed.
      var skBitmap = bitmap.ToSkBitmap();

      if (cursorInfo.HasValue)
      {
        dxOutput.LastCursorPosition = cursorVisible
          ? new Point(cursorInfo.Value.ptScreenPos.X, cursorInfo.Value.ptScreenPos.Y)
          : null;
      }

      dxOutput.LastCursorVisible = cursorVisible;
      dxOutput.LastCursorHandle = cursorHandle;

      if (captureCursor)
      {
        // Cache the clean (cursor-free) frame for use in cursor-only updates.
        dxOutput.SetLastCapturedSkBitmap(skBitmap.CopyEx());

        // Draw cursor directly on the result SKBitmap.
        using var canvas = new SKCanvas(skBitmap);
        dirtyRects = [.. dirtyRects, .. DrawCursorAndGetDirtyRects(canvas, dxOutput, display.LayoutBounds, cursorInfo)];
      }
      else
      {
        dxOutput.SetLastCapturedSkBitmap(null);
      }

      return CaptureResult.Ok(skBitmap, captureMode: DirectXCaptureMode, dirtyRects);
    }
    catch (COMException ex) when (ex.Message.StartsWith("The timeout value has elapsed"))
    {
      return CaptureResult.NoChanges(captureMode: DirectXCaptureMode);
    }
    catch (COMException ex)
    {
      _dxOutputGenerator.SetCurrentOutputFaulted();
      _logger.LogWarningDeduped(
        "Failed to capture with DirectX, falling back to BitBlt. Display: {DisplayId}",
        exception: ex,
        args: display.DeviceName);
      return CaptureResult.Fail(ex);
    }
    catch (Exception ex)
    {
      _dxOutputGenerator.SetCurrentOutputFaulted();
      _logger.LogErrorDeduped(
        "Failed to capture with DirectX, falling back to BitBlt. Display: {DisplayId}",
        exception: ex,
        args: display.DeviceName);
      return CaptureResult.Fail(ex);
    }
  }

  /// <summary>
  /// Retrieves the list of changed rectangles from the current DXGI frame.
  /// </summary>
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
    using var memoryDisposer = new CallbackDisposable(() => NativeMemory.Free(dirtyRectsPtr));

    outputDuplication.GetFrameDirtyRects(bufferSizeNeeded, dirtyRectsPtr, out _);

    for (var i = 0; i < numRects; i++)
    {
      dirtyRects[i] = dirtyRectsPtr[i].ToRectangle();
    }

    return dirtyRects;
  }

  /// <summary>
  /// Retrieves or generates a SkiaSharp version of the cursor, caching it for performance.
  /// </summary>
  private SKBitmap GetOrCacheCursorSkBitmap(IntPtr cursorHandle)
  {
    if (cursorHandle == _cachedCursorHandle && _cachedCursorSkBitmap is not null)
    {
      return _cachedCursorSkBitmap;
    }

    _cachedCursorSkBitmap?.Dispose();
    using var icon = Icon.FromHandle(cursorHandle);
    using var gdiBitmap = icon.ToBitmap();
    _cachedCursorSkBitmap = gdiBitmap.ToSkBitmap();
    _cachedCursorHandle = cursorHandle;
    return _cachedCursorSkBitmap;
  }

  /// <summary>
  /// Handles updates where only the cursor has moved by re-using the cached screen frame.
  /// </summary>
  private CaptureResult HandleCursorOnlyUpdate(DxOutput dxOutput, Rectangle captureArea, bool captureCursor)
  {
    if (!captureCursor)
    {
      return CaptureResult.NoChanges(captureMode: DirectXCaptureMode);
    }

    var cursorInfo = TryGetCursorInfo();
    var cursorScreenPos = cursorInfo.HasValue && cursorInfo.Value.flags.HasFlag(CURSORINFO_FLAGS.CURSOR_SHOWING)
      ? new Point(cursorInfo.Value.ptScreenPos.X, cursorInfo.Value.ptScreenPos.Y)
      : (Point?)null;
    var cursorVisible = cursorInfo?.flags.HasFlag(CURSORINFO_FLAGS.CURSOR_SHOWING) ?? false;
    var cursorHandle = cursorInfo?.hCursor ?? IntPtr.Zero;

    // Check if cursor state changed (position, visibility, or shape)
    var cursorStateChanged = cursorScreenPos != dxOutput.LastCursorPosition ||
                             cursorVisible != dxOutput.LastCursorVisible ||
                             cursorHandle != dxOutput.LastCursorHandle;

    if (!cursorStateChanged)
    {
      return CaptureResult.NoChanges(captureMode: DirectXCaptureMode);
    }

    // Update tracked cursor state
    dxOutput.LastCursorVisible = cursorVisible;
    dxOutput.LastCursorHandle = cursorHandle;
    dxOutput.LastCursorPosition = cursorScreenPos;

    if (dxOutput.LastCapturedSkBitmap is null)
    {
      return CaptureResult.NoChanges(captureMode: DirectXCaptureMode);
    }

    // Copy the cached clean frame and draw the cursor on top.
    // Ownership of resultBitmap is transferred to CaptureResult, so we must not dispose it here.
    var resultBitmap = dxOutput.LastCapturedSkBitmap.CopyEx();
    using var canvas = new SKCanvas(resultBitmap);
    var cursorDirtyRects = DrawCursorAndGetDirtyRects(canvas, dxOutput, captureArea, cursorInfo);
    return CaptureResult.Ok(resultBitmap, captureMode: DirectXCaptureMode, cursorDirtyRects);
  }

  /// <summary>
  /// Ensures the process is attached to the active input desktop.
  /// </summary>
  private void SwitchToInputDesktop()
  {
    var inputDesktopSwitchResult = _win32Interop.SwitchToInputDesktop();
    if (!inputDesktopSwitchResult && _inputDesktopSwitchResult)
    {
      _logger.LogWarningDeduped("Failed to switch to input desktop. This may be caused by hooks in the current desktop.");
    }
    _inputDesktopSwitchResult = inputDesktopSwitchResult;
  }

  /// <summary>
  /// Attempts to render the system cursor onto the provided canvas.
  /// </summary>
  private unsafe Rectangle TryDrawCursor(SKCanvas canvas, Rectangle captureArea, CURSORINFO? cursorInfo)
  {
    try
    {
      if (cursorInfo is null || !cursorInfo.Value.flags.HasFlag(CURSORINFO_FLAGS.CURSOR_SHOWING))
      {
        return Rectangle.Empty;
      }

      var ci = cursorInfo.Value;
      using var icon = Icon.FromHandle(ci.hCursor);

      uint hotspotX = 0;
      uint hotspotY = 0;
      var hicon = new HICON(icon.Handle);
      var iconInfoPtr = stackalloc ICONINFO[1];
      if (PInvoke.GetIconInfo(hicon, iconInfoPtr))
      {
        try
        {
          hotspotX = iconInfoPtr->xHotspot;
          hotspotY = iconInfoPtr->yHotspot;
        }
        finally
        {
          if (iconInfoPtr->hbmMask.Value != null)
          {
            PInvoke.DeleteObject(iconInfoPtr->hbmMask);
          }
          if (iconInfoPtr->hbmColor.Value != null)
          {
            PInvoke.DeleteObject(iconInfoPtr->hbmColor);
          }
        }
      }

      var x = (int)(ci.ptScreenPos.X - captureArea.Left - hotspotX);
      var y = (int)(ci.ptScreenPos.Y - captureArea.Top - hotspotY);

      var targetArea = new Rectangle(x, y, icon.Width, icon.Height);
      var localBounds = new Rectangle(0, 0, captureArea.Width, captureArea.Height);
      if (!localBounds.IntersectsWith(targetArea))
      {
        _logger.LogDebugDeduped("Cursor is outside of capture area. Skipping.");
        return Rectangle.Empty;
      }

      var cursorBitmap = GetOrCacheCursorSkBitmap(ci.hCursor);
      canvas.DrawBitmap(cursorBitmap, x, y);

      return Rectangle.Intersect(targetArea, localBounds);
    }
    catch (Exception ex)
    {
      _logger.LogDebugDeduped(
        "Failed to draw cursor on capture.",
        exception: ex);
      return Rectangle.Empty;
    }
  }

  /// <summary>
  /// Safely retrieves metadata for the system cursor.
  /// </summary>
  private CURSORINFO? TryGetCursorInfo()
  {
    try
    {
      var ci = new CURSORINFO();
      ci.cbSize = (uint)Marshal.SizeOf(ci);
      if (!PInvoke.GetCursorInfo(ref ci))
      {
        return null;
      }
      return ci;
    }
    catch
    {
      return null;
    }
  }
}