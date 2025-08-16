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
  public static SKBitmap ToSKBitmap(this Bitmap bitmap)
  {
    var info = new SKImageInfo(bitmap.Width, bitmap.Height);
    var sKBitmap = new SKBitmap(info);
    using SKPixmap pixmap = sKBitmap.PeekPixels();
    bitmap.ToSKPixmap(pixmap);
    return sKBitmap;
  }

  public static void ToSKPixmap(this Bitmap bitmap, SKPixmap pixmap)
  {
    if (pixmap.ColorType == SKImageInfo.PlatformColorType)
    {
      var info = pixmap.Info;
      using Bitmap image = new(info.Width, info.Height, info.RowBytes, PixelFormat.Format32bppPArgb, pixmap.GetPixels());
      using Graphics graphics = Graphics.FromImage(image);
      graphics.Clear(Color.Transparent);
      graphics.DrawImageUnscaled(bitmap, 0, 0);
      return;
    }

    using SKImage sKImage = bitmap.ToSKImage();
    sKImage.ReadPixels(pixmap, 0, 0);
  }

  public static SKImage ToSKImage(this Bitmap bitmap)
  {
    var info = new SKImageInfo(bitmap.Width, bitmap.Height);
    var sKImage = SKImage.Create(info);
    using SKPixmap pixmap = sKImage.PeekPixels();
    bitmap.ToSKPixmap(pixmap);
    return sKImage;
  }
}
