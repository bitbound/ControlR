using ControlR.DesktopClient.Common.Extensions;
using ControlR.Libraries.Shared.Primitives;
using SkiaSharp;

namespace ControlR.DesktopClient.Common.Services;

public interface IImageUtility
{
  SKBitmap CropBitmap(SKBitmap bitmap, SKRect cropArea);
  SKBitmap DownscaleBitmap(SKBitmap bitmap, double scale);
  byte[] Encode(SKBitmap bitmap, SKEncodedImageFormat format, int quality = 80);
  byte[] EncodeJpeg(SKBitmap bitmap, int quality, bool compressOutput = true);
  Result<SKRect> GetChangedArea(SKBitmap? currentFrame, SKBitmap? previousFrame, bool forceFullscreen = false);
  public bool IsEmpty(SKBitmap bitmap);
}

public class ImageUtility : IImageUtility
{
  public SKBitmap CropBitmap(SKBitmap bitmap, SKRect cropArea)
  {
    var cropped = new SKBitmap((int)cropArea.Width, (int)cropArea.Height);
    using var canvas = new SKCanvas(cropped);
    canvas.DrawBitmap(
        bitmap,
        cropArea,
        new SKRect(0, 0, cropArea.Width, cropArea.Height));
    return cropped;
  }

  public SKBitmap DownscaleBitmap(SKBitmap bitmap, double scale)
  {
    var newWidth = (int)(bitmap.Width * scale);
    var newHeight = (int)(bitmap.Height * scale);
    var imageInfo = new SKImageInfo(newWidth, newHeight);
    return bitmap.Resize(imageInfo, default(SKSamplingOptions));
  }

  public byte[] Encode(SKBitmap bitmap, SKEncodedImageFormat format, int quality = 80)
  {
    using var ms = new MemoryStream();
    bitmap.Encode(ms, format, quality);
    return ms.ToArray();
  }

  public byte[] EncodeJpeg(SKBitmap bitmap, int quality, bool compressOutput = true)
  {
    using var ms = new MemoryStream();
    bitmap.Encode(ms, SKEncodedImageFormat.Jpeg, quality);
    return ms.ToArray();
  }

  public Result<SKRect> GetChangedArea(SKBitmap? currentFrame, SKBitmap? previousFrame, bool forceFullscreen = false)
  {
    if (currentFrame is null)
    {
      return Result.Ok(SKRect.Empty);
    }

    if (previousFrame is null || forceFullscreen)
    {
      return Result.Ok(currentFrame.ToRect());
    }

    if (currentFrame.Height != previousFrame.Height || currentFrame.Width != previousFrame.Width)
    {
      return Result.Fail<SKRect>("Bitmaps are not of equal dimensions.");
    }

    if (currentFrame.BytesPerPixel != previousFrame.BytesPerPixel)
    {
      return Result.Fail<SKRect>("Bitmaps do not have the same pixel size.");
    }

    var width = currentFrame.Width;
    var height = currentFrame.Height;
    var left = int.MaxValue;
    var top = int.MaxValue;
    var right = int.MinValue;
    var bottom = int.MinValue;

    var bytesPerPixel = currentFrame.BytesPerPixel;

    try
    {
      unsafe
      {
        var scan1 = (byte*)currentFrame.GetPixels().ToPointer();
        var scan2 = (byte*)previousFrame.GetPixels().ToPointer();

        for (var row = 0; row < height; row++)
        {
          for (var column = 0; column < width; column++)
          {
            var index = row * width * bytesPerPixel + column * bytesPerPixel;

            var data1 = scan1 + index;
            var data2 = scan2 + index;

            if (data1[0] == data2[0] &&
                data1[1] == data2[1] &&
                data1[2] == data2[2] &&
                data1[3] == data2[3])
            {
              continue;
            }

            top = Math.Min(top, row);
            bottom = Math.Max(bottom, row);
            left = Math.Min(left, column);
            right = Math.Max(right, column);
          }
        }

        if (left <= right && top <= bottom)
        {
          left = Math.Max(left - 2, 0);
          top = Math.Max(top - 2, 0);
          right = Math.Min(right + 2, width);
          bottom = Math.Min(bottom + 2, height);

          return Result.Ok(new SKRect(left, top, right, bottom));
        }
        else
        {
          return Result.Ok(SKRect.Empty);
        }
      }
    }
    catch (Exception ex)
    {
      return Result.Fail<SKRect>(ex);
    }
  }
  public bool IsEmpty(SKBitmap bitmap)
  {
    var height = bitmap.Height;
    var width = bitmap.Width;
    var bytesPerPixel = bitmap.BytesPerPixel;

    try
    {
      unsafe
      {
        var scan = (byte*)bitmap.GetPixels().ToPointer();

        for (var row = 0; row < height; row++)
        {
          for (var column = 0; column < width; column++)
          {
            var index = row * width * bytesPerPixel + column * bytesPerPixel;

            var data = scan + index;

            if (data[0] == 0 &&
                data[1] == 0 &&
                data[2] == 0 &&
                data[3] == 0)
            {
              continue;
            }

            return false;
          }
        }

        return true;
      }
    }
    catch
    {
      return true;
    }
  }
}
