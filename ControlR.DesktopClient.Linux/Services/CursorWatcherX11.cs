using Bitbound.SimpleMessenger;
using ControlR.DesktopClient.Common.Messages;
using ControlR.DesktopClient.Common.Services;
using ControlR.Libraries.NativeInterop.Unix.Linux;
using ControlR.Libraries.Shared.Enums;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SkiaSharp;
using System.Runtime.InteropServices;

namespace ControlR.DesktopClient.Linux.Services;

internal class CursorWatcherX11(
  IMessenger messenger,
  IImageUtility imageUtility,
  ILogger<CursorWatcherX11> logger) : BackgroundService
{
  private readonly IImageUtility _imageUtility = imageUtility;
  private readonly ILogger<CursorWatcherX11> _logger = logger;
  private readonly IMessenger _messenger = messenger;

  private IntPtr _display = IntPtr.Zero;
  private string? _lastCursorBase64;
  private ulong _lastCursorSerial = 0;

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    _display = LibX11.XOpenDisplay(null);
    if (_display == IntPtr.Zero)
    {
      _logger.LogError("Failed to open X11 display");
      return;
    }

    try
    {
      // Check if XFixes extension is available
      if (LibXfixes.XFixesQueryVersion(_display, out int major, out int minor) == 0)
      {
        _logger.LogError("XFixes extension not available");
        return;
      }

      _logger.LogInformation("XFixes version: {Major}.{Minor}", major, minor);

      while (!stoppingToken.IsCancellationRequested)
      {
        try
        {
          await Task.Delay(TimeSpan.FromMilliseconds(10), stoppingToken);
          await CheckCursorChange();
        }
        catch (OperationCanceledException)
        {
          _logger.LogInformation("Cursor watch aborted. Application shutting down.");
          break;
        }
        catch (Exception ex)
        {
          _logger.LogError(ex, "Error while getting mouse cursor.");
        }
      }
    }
    finally
    {
      if (_display != IntPtr.Zero)
      {
        LibX11.XCloseDisplay(_display);
        _display = IntPtr.Zero;
      }
    }
  }

  private async Task CheckCursorChange()
  {
    var cursorImagePtr = LibXfixes.XFixesGetCursorImage(_display);
    if (cursorImagePtr == IntPtr.Zero)
    {
      return;
    }

    try
    {
      var cursorImage = Marshal.PtrToStructure<LibXfixes.XFixesCursorImage>(cursorImagePtr);

      // Check if cursor has changed by comparing serial numbers
      if (cursorImage.cursor_serial == _lastCursorSerial)
      {
        return;
      }

      _lastCursorSerial = cursorImage.cursor_serial;

      // Convert cursor to PNG and base64
      var cursorBase64 = ConvertCursorToPngBase64(cursorImage);

      // Only broadcast if the actual image data changed
      if (cursorBase64 != _lastCursorBase64)
      {
        _lastCursorBase64 = cursorBase64;
        var changeMessage = new CursorChangedMessage(
          PointerCursor.Custom,
          cursorBase64,
          cursorImage.xhot,
          cursorImage.yhot);

        await _messenger.Send(changeMessage);

        _logger.LogDebug("Cursor changed. Broadcasted new cursor image.");
      }
    }
    finally
    {
      LibX11.XFree(cursorImagePtr);
    }
  }

  private unsafe string? ConvertCursorToPngBase64(LibXfixes.XFixesCursorImage cursorImage)
  {
    try
    {
      if (cursorImage.width <= 0 || cursorImage.height <= 0 || cursorImage.pixels == IntPtr.Zero)
      {
        _logger.LogDebug(
          "Invalid cursor dimensions: {Width}x{Height}", 
          cursorImage.width,
          cursorImage.height);
        return null;
      }

      _logger.LogDebug(
        "Converting cursor: {Width}x{Height}, hotspot: ({XHot},{YHot})",
        cursorImage.width,
        cursorImage.height,
        cursorImage.xhot,
        cursorImage.yhot);

      var totalPixels = cursorImage.width * cursorImage.height;
      
      // X11 cursor pixels are stored as unsigned long (64-bit on 64-bit Linux systems)
      // Each unsigned long contains ARGB pixel data in the lower 32 bits
      var srcPtr = (ulong*)cursorImage.pixels;
      
      var pixelData = new uint[totalPixels];

      // Convert X11 ARGB format to BGRA format for SkiaSharp
      for (int i = 0; i < totalPixels; i++)
      {
        // X11 stores as 64-bit unsigned long, but pixel data is in lower 32 bits
        var argbPixel = (uint)(srcPtr[i] & 0xFFFFFFFF);
        
        // Extract ARGB components
        var a = (byte)((argbPixel >> 24) & 0xFF);
        var r = (byte)((argbPixel >> 16) & 0xFF);
        var g = (byte)((argbPixel >> 8) & 0xFF);
        var b = (byte)(argbPixel & 0xFF);
        
        // Convert to BGRA and ensure proper alpha handling
        // If alpha is 0, make pixel transparent; otherwise ensure it's visible
        if (a == 0)
        {
          pixelData[i] = 0; // Fully transparent
        }
        else
        {
          // Convert ARGB to BGRA: Blue in lowest byte, Green, Red, Alpha in highest byte
          pixelData[i] = (uint)(a << 24 | r << 16 | g << 8 | b);
        }
      }

      // Create bitmap with converted pixel data
      using var bitmap = new SKBitmap(cursorImage.width, cursorImage.height, SKColorType.Bgra8888, SKAlphaType.Unpremul);
      
      fixed (uint* dataPtr = pixelData)
      {
        bitmap.SetPixels((IntPtr)dataPtr);
      }

      var pngBytes = _imageUtility.Encode(bitmap, SKEncodedImageFormat.Png);
      _logger.LogDebug("Successfully converted cursor to PNG: {ByteCount} bytes", pngBytes.Length);
      
      return Convert.ToBase64String(pngBytes);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to convert cursor to PNG base64");
      return null;
    }
  }
}