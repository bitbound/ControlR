using SkiaSharp;
using System.Drawing;

namespace ControlR.DesktopClient.Common.Extensions;

public static class SkiaSharpExtensions
{
  /// <summary>
  /// The built-in Copy method in SkiaSharp has a memory leak.
  /// </summary>
  /// <param name="bitmap"></param>
  /// <returns></returns>
  public static SKBitmap CopyEx(this SKBitmap bitmap)
  {
    var clone = new SKBitmap(bitmap.Info);
    using var canvas = new SKCanvas(clone);
    canvas.DrawBitmap(bitmap, 0, 0);
    return clone;
  }

  public static Rectangle ToRectangle(this SKRect rect)
  {
    return new Rectangle(
      (int)rect.Left,
      (int)rect.Top,
      (int)(rect.Right - rect.Left),
      (int)(rect.Bottom - rect.Top));
  }
  public static SKRect ToSkRect(this SKBitmap bitmap)
  {
    return new SKRect(0, 0, bitmap.Width, bitmap.Height);
  }

  public static SKRect ToSkRect(this Rectangle rectangle)
  {
    return new SKRect(
      rectangle.X,
      rectangle.Y,
      rectangle.X + rectangle.Width,
      rectangle.Y + rectangle.Height);
  }
}
