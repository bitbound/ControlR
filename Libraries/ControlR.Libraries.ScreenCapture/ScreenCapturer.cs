using ControlR.Libraries.ScreenCapture.Extensions;
using ControlR.Libraries.ScreenCapture.Helpers;
using ControlR.Libraries.ScreenCapture.Models;
using Microsoft.Extensions.Logging;
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

namespace ControlR.Libraries.ScreenCapture;

public interface IScreenCapturer
{
    /// <summary>
    /// Gets a capture of a specific display.
    /// </summary>
    /// <param name="display">The display to capture.  Retrieve current displays from <see cref="GetDisplays"/>. </param>
    /// <param name="captureCursor">Whether to include the cursor in the capture.</param>
    /// <param name="tryUseDirectX">Whether to attempt using DirecX (DXGI) for getting the capture.</param>
    /// <param name="directXTimeout">
    /// The amount of time, in milliseconds, to allow DirectX to attempt to capture the screen.
    /// If no screen changes have occurred within this time, the capture will time out.
    /// </param>
    /// <param name="allowFallbackToBitBlt">
    /// Whether to allow fallback to BitBlt for capture, which is not DirectX-accelerated, in the event of timeout or exception.
    /// </param>
    /// <returns>
    /// A result object indicating whether the capture was successful.
    /// If successful, the result will contain the <see cref="Bitmap"/> of the capture.
    /// </returns>
    CaptureResult Capture(
        DisplayInfo targetDisplay,
        bool captureCursor = true,
        bool tryUseDirectX = true,
        int directXTimeout = 50,
        bool allowFallbackToBitBlt = true);

    /// <summary>
    /// Gets a capture of all displays.  This method is not DirectX-accelerated.
    /// </summary>
    /// <param name="captureCursor">Whether to include the cursor in the capture.</param>
    /// <returns>
    /// A result object indicating whether the capture was successful.
    /// If successful, the result will contain the <see cref="Bitmap"/> of the capture.
    /// </returns>
    CaptureResult Capture(bool captureCursor = true);

    /// <summary>
    /// Return info about the connected displays.
    /// </summary>
    /// <returns></returns>
    IEnumerable<DisplayInfo> GetDisplays();

    /// <summary>
    /// Returns the area encompassing all displays.
    /// </summary>
    Rectangle GetVirtualScreenBounds();
}

internal sealed class ScreenCapturer(
    IBitmapUtility _bitmapUtility,
    IDxOutputGenerator _dxOutputGenerator,
    ILogger<ScreenCapturer> _logger) : IScreenCapturer
{
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

            var result = GetDirectXCapture(display);

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

    public IEnumerable<DisplayInfo> GetDisplays() => DisplaysEnumerationHelper.GetDisplays();

    public Rectangle GetVirtualScreenBounds()
    {
        var width = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CXVIRTUALSCREEN);
        var height = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CYVIRTUALSCREEN);
        var left = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_XVIRTUALSCREEN);
        var top = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_YVIRTUALSCREEN);
        return new Rectangle(left, top, width, height);
    }

    internal CaptureResult GetBitBltCapture(Rectangle captureArea, bool captureCursor)
    {
        var hwnd = HWND.Null;
        var screenDc = new HDC();

        try
        {
            hwnd = PInvoke.GetDesktopWindow();
            screenDc = PInvoke.GetWindowDC(hwnd);

            var bitmap = new Bitmap(captureArea.Width, captureArea.Height);
            using var graphics = Graphics.FromImage(bitmap);
            var targetDc = graphics.GetHdc();

            var bitBltResult = PInvoke.BitBlt(new HDC(targetDc), 0, 0, captureArea.Width, captureArea.Height,
                screenDc, captureArea.X, captureArea.Y, ROP_CODE.SRCCOPY);

            graphics.ReleaseHdc(targetDc);

            if (!bitBltResult)
            {
                return CaptureResult.Fail("BitBlt function failed.");
            }


            if (captureCursor)
            {
                // Get cursor information to draw on the screenshot.
                var ci = new CURSORINFO();
                ci.cbSize = (uint)Marshal.SizeOf(ci);
                PInvoke.GetCursorInfo(ref ci);
                if (ci.flags == CURSORINFO_FLAGS.CURSOR_SHOWING)
                {
                    using var icon = Icon.FromHandle(ci.hCursor);
                    var virtualScreen = GetVirtualScreenBounds();
                    graphics.DrawIcon(
                        icon,
                        ci.ptScreenPos.X - virtualScreen.Left - captureArea.Left,
                        ci.ptScreenPos.Y - virtualScreen.Top - captureArea.Top);
                }
            }

            return CaptureResult.Ok(bitmap, false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting capture with BitBlt.");
            return CaptureResult.Fail(ex);
        }
        finally
        {
            _ = PInvoke.ReleaseDC(hwnd, screenDc);
        }
    }

    internal CaptureResult GetDirectXCapture(DisplayInfo display)
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
                catch { }
                return CaptureResult.NoAccumulatedFrames();
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
                default:
                    break;
            }
            return CaptureResult.Ok(bitmap, true, dirtyRects);
        }
        catch (COMException ex) when (ex.Message.StartsWith("The timeout value has elapsed"))
        {
            return CaptureResult.TimedOut();
        }
        catch (COMException ex)
        {
            _dxOutputGenerator.MarkFaulted(dxOutput);
            _logger.LogWarning("DirectX outputs need to be refreshed.");
            return CaptureResult.Fail(ex);
        }
        catch (Exception ex)
        {
            _dxOutputGenerator.MarkFaulted(dxOutput);
            _logger.LogError(ex, "Error while capturing with DirectX.");
            return CaptureResult.Fail(ex);
        }
        finally
        {
            try
            {
                dxOutput.OutputDuplication.ReleaseFrame();
            }
            catch { }
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
        catch { }

        if (bufferSizeNeeded == 0)
        {
            return [];
        }

        var numRects = (int)(bufferSizeNeeded / rectSize);
        var dirtyRects = new Rectangle[numRects];

        RECT* dirtyRectsPtr = stackalloc RECT[numRects];
        outputDuplication.GetFrameDirtyRects(bufferSizeNeeded, dirtyRectsPtr, out _);

        for (var i = 0; i < numRects; i++)
        {
            dirtyRects[i] = dirtyRectsPtr[i].ToRectangle();
        }

        return dirtyRects;
    }
}
