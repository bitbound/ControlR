using SkiaSharp;
using System.Drawing;

namespace ControlR.DesktopClient.Common.Extensions;

public static class SkiaSharpExtensions
{
  public static SKRect ToRect(this SKBitmap bitmap)
  {
    return new SKRect(0, 0, bitmap.Width, bitmap.Height);
  }

  public static SKRect ToRect(this Rectangle rectangle)
  {
    return new SKRect(
      rectangle.X,
      rectangle.Y,
      rectangle.X + rectangle.Width,
      rectangle.Y + rectangle.Height);
  }

  public static Rectangle ToRectangle(this SKRect rect)
  {
    return new Rectangle(
      (int)rect.Left,
      (int)rect.Top,
      (int)(rect.Right - rect.Left),
      (int)(rect.Bottom - rect.Top));
  }
}
