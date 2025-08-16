using ControlR.Libraries.NativeInterop.Unix.MacOs;
using SkiaSharp;
using System.Drawing;
using System.Runtime.InteropServices;

namespace ControlR.DesktopClient.Mac.Helpers;

internal static class CoreGraphicsHelper
{
  public static Rectangle ToRectangle(this CoreGraphics.CGRect cgRect)
  {
    return new Rectangle(
      (int)cgRect.X,
      (int)cgRect.Y,
      (int)cgRect.Width,
      (int)cgRect.Height);
  }

  public static CoreGraphics.CGRect ToCGRect(this Rectangle rectangle)
  {
    return new CoreGraphics.CGRect(
      rectangle.X,
      rectangle.Y,
      rectangle.Width,
      rectangle.Height);
  }

  public static SKBitmap? CGImageToSKBitmap(nint cgImageRef)
  {
    if (cgImageRef == nint.Zero)
      return null;

    try
    {
      var width = (int)CoreGraphics.CGImageGetWidth(cgImageRef);
      var height = (int)CoreGraphics.CGImageGetHeight(cgImageRef);
      var bitsPerComponent = (int)CoreGraphics.CGImageGetBitsPerComponent(cgImageRef);
      var bitsPerPixel = (int)CoreGraphics.CGImageGetBitsPerPixel(cgImageRef);
      var bytesPerRow = (int)CoreGraphics.CGImageGetBytesPerRow(cgImageRef);

      var dataProvider = CoreGraphics.CGImageGetDataProvider(cgImageRef);
      if (dataProvider == nint.Zero)
        return null;

      var data = CoreGraphics.CGDataProviderCopyData(dataProvider);
      if (data == nint.Zero)
        return null;

      try
      {
        var dataPtr = CoreGraphics.CFDataGetBytePtr(data);
        var dataLength = (int)CoreGraphics.CFDataGetLength(data);

        if (dataPtr == nint.Zero || dataLength == 0)
          return null;

        // Create SKBitmap
        var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        var skBitmap = new SKBitmap(info);

        // Copy data to SKBitmap
        unsafe
        {
          var skPixels = (byte*)skBitmap.GetPixels();
          var sourcePixels = (byte*)dataPtr;

          // For macOS, CGImage typically uses BGRA format
          if (bitsPerPixel == 32 && bitsPerComponent == 8)
          {
            for (int y = 0; y < height; y++)
            {
              var sourceRow = sourcePixels + (y * bytesPerRow);
              var destRow = skPixels + (y * skBitmap.RowBytes);

              for (int x = 0; x < width; x++)
              {
                var sourceIndex = x * 4;
                var destIndex = x * 4;

                // macOS CGImage: BGRA -> SkiaSharp: BGRA (same format)
                destRow[destIndex] = sourceRow[sourceIndex];     // B
                destRow[destIndex + 1] = sourceRow[sourceIndex + 1]; // G
                destRow[destIndex + 2] = sourceRow[sourceIndex + 2]; // R
                destRow[destIndex + 3] = sourceRow[sourceIndex + 3]; // A
              }
            }
          }
          else
          {
            // Fallback: copy raw data
            Buffer.MemoryCopy(sourcePixels, skPixels, dataLength, Math.Min(dataLength, skBitmap.ByteCount));
          }
        }

        return skBitmap;
      }
      finally
      {
        CoreGraphics.CFRelease(data);
      }
    }
    catch
    {
      return null;
    }
  }

  public static void ReleaseCGImage(nint cgImageRef)
  {
    if (cgImageRef != nint.Zero)
    {
      CoreGraphics.CFRelease(cgImageRef);
    }
  }
}
