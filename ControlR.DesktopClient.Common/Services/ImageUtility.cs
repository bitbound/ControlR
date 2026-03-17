using System.Drawing;
using ControlR.DesktopClient.Common.Extensions;
using ControlR.Libraries.Shared.Primitives;
using SkiaSharp;

namespace ControlR.DesktopClient.Common.Services;

public interface IImageUtility
{
  Result<SKRect[]> ClampToGridSections(Size bitmapSize, SKRect[] changedAreas, uint gridColumns = 4, uint gridRows = 2);
  SKBitmap CropBitmap(SKBitmap bitmap, SKRect cropArea);
  SKBitmap DownscaleBitmap(SKBitmap bitmap, double scale);
  byte[] Encode(SKBitmap bitmap, SKEncodedImageFormat format, int quality = 80);
  byte[] EncodeJpeg(SKBitmap bitmap, int quality, bool compressOutput = true);
  Result<SKRect> GetChangedArea(SKBitmap? currentFrame, SKBitmap? previousFrame, bool forceFullscreen = false);
  Result<SKRect[]> GetChangedAreas(SKBitmap? currentFrame, SKBitmap? previousFrame, uint gridColumns = 4, uint gridRows = 2, bool forceFullscreen = false);
  bool IsEmpty(SKBitmap bitmap);
}

public class ImageUtility : IImageUtility
{
  public Result<SKRect[]> ClampToGridSections(Size bitmapSize, SKRect[] changedAreas, uint gridColumns = 4, uint gridRows = 2)
  {
    if (gridColumns == 0 || gridRows == 0)
    {
      return Result.Fail<SKRect[]>("gridColumns and gridRows must be at least 1.");
    }

    if (changedAreas.Length == 0)
    {
      return Result.Ok(Array.Empty<SKRect>());
    }

    var width = bitmapSize.Width;
    var height = bitmapSize.Height;

    if (width < gridColumns || height < gridRows)
    {
      return Result.Fail<SKRect[]>($"Bitmap dimensions are smaller than the grid size. Bitmap size: {width}x{height}. Grid size: {gridColumns}x{gridRows}");
    }

    var sectionWidth = width / (int)gridColumns;
    var sectionHeight = height / (int)gridRows;

    var resultAreas = new List<SKRect>();

    foreach (var area in changedAreas)
    {
      if (area.IsEmpty)
      {
        continue;
      }

      var left = (int)area.Left;
      var top = (int)area.Top;
      var right = (int)area.Right;
      var bottom = (int)area.Bottom;

      var startCol = Math.Max(left / sectionWidth, 0);
      var startRow = Math.Max(top / sectionHeight, 0);
      var endCol = Math.Min(right / sectionWidth, (int)gridColumns - 1);
      var endRow = Math.Min(bottom / sectionHeight, (int)gridRows - 1);

      for (var row = startRow; row <= endRow; row++)
      {
        for (var col = startCol; col <= endCol; col++)
        {
          var gridLeft = col * sectionWidth;
          var gridTop = row * sectionHeight;
          var gridRight = col == gridColumns - 1 ? width : (col + 1) * sectionWidth;
          var gridBottom = row == gridRows - 1 ? height : (row + 1) * sectionHeight;

          var clampedLeft = Math.Max(left, gridLeft);
          var clampedTop = Math.Max(top, gridTop);
          var clampedRight = Math.Min(right, gridRight);
          var clampedBottom = Math.Min(bottom, gridBottom);

          if (clampedLeft < clampedRight && clampedTop < clampedBottom)
          {
            resultAreas.Add(new SKRect(clampedLeft, clampedTop, clampedRight, clampedBottom));
          }
        }
      }
    }

    return Result.Ok(resultAreas.ToArray());
  }

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
      return Result.Ok(currentFrame.ToSkRect());
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

  public Result<SKRect[]> GetChangedAreas(SKBitmap? currentFrame, SKBitmap? previousFrame, uint gridColumns = 4, uint gridRows = 2, bool forceFullscreen = false)
  {
    if (gridColumns == 0 || gridRows == 0)
    {
      return Result.Fail<SKRect[]>("gridColumns and gridRows must be at least 1.");
    }

    if (currentFrame is null)
    {
      return Result.Ok(Array.Empty<SKRect>());
    }

    if (previousFrame is null || forceFullscreen)
    {
      return Result.Ok<SKRect[]>([currentFrame.ToSkRect()]);
    }

    if (currentFrame.Height != previousFrame.Height || currentFrame.Width != previousFrame.Width)
    {
      return Result.Fail<SKRect[]>("Bitmaps are not of equal dimensions.");
    }

    if (currentFrame.BytesPerPixel != previousFrame.BytesPerPixel)
    {
      return Result.Fail<SKRect[]>("Bitmaps do not have the same pixel size.");
    }

    if (currentFrame.Width < gridColumns || currentFrame.Height <  gridRows)
    {
      return Result.Fail<SKRect[]>($"Bitmap dimensions are smaller than the grid size. Bitmap size: {currentFrame.Width}x{currentFrame.Height}. Grid size: {gridColumns}x{gridRows}");
    }

    var width = currentFrame.Width;
    var height = currentFrame.Height;
    var sectionWidth = width / (int)gridColumns;
    var sectionHeight = height / (int)gridRows;

    var sectionCount = gridColumns * gridRows;
    var results = new SKRect[sectionCount];

    Parallel.For(0, sectionCount, index =>
    {
      var col = (int)(index % gridColumns);
      var row = (int)(index / gridColumns);
      var x = col * sectionWidth;
      var y = row * sectionHeight;
      var w = col == gridColumns - 1 ? width - x : sectionWidth;
      var h = row == gridRows - 1 ? height - y : sectionHeight;
      results[index] = GetChangedAreaForSection(currentFrame, previousFrame, x, y, w, h);
    });

    return Result.Ok(results);
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

  private static SKRect GetChangedAreaForSection(SKBitmap currentFrame, SKBitmap previousFrame, int startX, int startY, int sectionWidth, int sectionHeight)
  {
    var width = currentFrame.Width;
    var height = currentFrame.Height;
    var bytesPerPixel = currentFrame.BytesPerPixel;

    var left = int.MaxValue;
    var top = int.MaxValue;
    var right = int.MinValue;
    var bottom = int.MinValue;

    try
    {
      unsafe
      {
        var scan1 = (byte*)currentFrame.GetPixels().ToPointer();
        var scan2 = (byte*)previousFrame.GetPixels().ToPointer();

        var endX = Math.Min(startX + sectionWidth, width);
        var endY = Math.Min(startY + sectionHeight, height);

        for (var row = startY; row < endY; row++)
        {
          for (var column = startX; column < endX; column++)
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
          left = Math.Max(left - 2, startX);
          top = Math.Max(top - 2, startY);
          right = Math.Min(right + 2, endX);
          bottom = Math.Min(bottom + 2, endY);

          return new SKRect(left, top, right, bottom);
        }
        else
        {
          return SKRect.Empty;
        }
      }
    }
    catch
    {
      return SKRect.Empty;
    }
  }
}
