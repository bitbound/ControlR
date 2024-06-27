using System.Drawing;
using Windows.Graphics.Imaging;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace ControlR.Libraries.ScreenCapture.Extensions;
public static class BitmapExtensions
{
    public static Rectangle ToRectangle(this Bitmap bitmap)
    {
        return new Rectangle(0, 0, bitmap.Width, bitmap.Height);
    }
    public static SoftwareBitmap ToSoftwareBitmap(this Bitmap bitmap)
    {
        var bd = bitmap.LockBits(bitmap.ToRectangle(), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        var sbBuffer = new byte[bd.Stride * bd.Height];
        Marshal.Copy(bd.Scan0, sbBuffer, 0, sbBuffer.Length);
        bitmap.UnlockBits(bd);

        return new SoftwareBitmap(BitmapPixelFormat.Bgra8, bitmap.Width, bitmap.Height);
    }
}
