using ControlR.Libraries.Shared.Extensions;
using SkiaSharp;
using System.Drawing;
using System.Drawing.Imaging;

namespace ControlR.DesktopClient.Windows.Extensions;
public static class BitmapExtensions
{
  public static Rectangle ToRectangle(this Bitmap bitmap)
  {
    return new Rectangle(0, 0, bitmap.Width, bitmap.Height);
  }
  public static SKBitmap ToSkBitmap(this Bitmap bitmap)
  {
    var info = new SKImageInfo(bitmap.Width, bitmap.Height);
    var sKBitmap = new SKBitmap(info);
    using var pixmap = sKBitmap.PeekPixels();
    bitmap.ToSkPixmap(pixmap);
    return sKBitmap;
  }

  public static SKImage ToSkImage(this Bitmap bitmap)
  {
    var info = new SKImageInfo(bitmap.Width, bitmap.Height);
    using var sKImage = SKImage.Create(info).AsMaybeDisposable();
    using var pixmap = sKImage.Value.PeekPixels();
    bitmap.ToSkPixmap(pixmap);
    return sKImage.Suppress();
  }

  public static void ToSkPixmap(this Bitmap bitmap, SKPixmap pixmap)
  {
    if (pixmap.ColorType == SKImageInfo.PlatformColorType)
    {
      var info = pixmap.Info;
      using Bitmap image = new(info.Width, info.Height, info.RowBytes, PixelFormat.Format32bppPArgb, pixmap.GetPixels());
      using var graphics = Graphics.FromImage(image);
      graphics.Clear(Color.Transparent);
      graphics.DrawImageUnscaled(bitmap, 0, 0);
      return;
    }

    using var sKImage = bitmap.ToSkImage();
    sKImage.ReadPixels(pixmap, 0, 0);
  }
}
