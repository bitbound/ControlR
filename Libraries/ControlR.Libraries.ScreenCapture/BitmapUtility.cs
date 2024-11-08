using ControlR.Libraries.ScreenCapture.Primitives;
using System.Drawing;
using System.Drawing.Imaging;
using Windows.Graphics.Imaging;

namespace ControlR.Libraries.ScreenCapture;

public interface IBitmapUtility
{
    byte[] Encode(Bitmap bitmap, ImageFormat format);
    byte[] EncodeJpeg(Bitmap bitmap, int quality);
    Bitmap CropBitmap(Bitmap bitmap, Rectangle cropArea);
    ResultEx<Rectangle> GetChangedArea(Bitmap currentFrame, Bitmap? previousFrame, bool forceFullscreen = false);
    Task<byte[]> EncodeJpegWinRt(SoftwareBitmap bitmap, double quality);
    public bool IsEmpty(Bitmap bitmap);
}

public class BitmapUtility : IBitmapUtility
{
    private readonly ImageCodecInfo _jpegEncoder = ImageCodecInfo.GetImageEncoders().First(x => x.FormatID == ImageFormat.Jpeg.Guid);

    public Bitmap CropBitmap(Bitmap bitmap, Rectangle cropArea)
    {
        return bitmap.Clone(cropArea, bitmap.PixelFormat);
    }

    public byte[] Encode(Bitmap bitmap, ImageFormat format)
    {
        using var ms = new MemoryStream();
        bitmap.Save(ms, format);
        return ms.ToArray();
    }

    public byte[] EncodeJpeg(Bitmap bitmap, int quality)
    {
        using var ms = new MemoryStream();
        using var encoderParams = new EncoderParameters(1);
        encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, quality);
        bitmap.Save(ms, _jpegEncoder, encoderParams);
        return ms.ToArray();
    }

    public async Task<byte[]> EncodeJpegWinRt(SoftwareBitmap bitmap, double quality)
    {
        var propertySet = new BitmapPropertySet();
        var qualityValue = new BitmapTypedValue(quality, Windows.Foundation.PropertyType.Single);
        propertySet.Add("ImageQuality", qualityValue);

        using var ms = new MemoryStream();
        using var ras = ms.AsRandomAccessStream();

        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, ras, propertySet);

        encoder.SetSoftwareBitmap(bitmap);
        await encoder.FlushAsync();

        return ms.ToArray();
    }

    public ResultEx<Rectangle> GetChangedArea(Bitmap currentFrame, Bitmap? previousFrame, bool forceFullscreen = false)
    {
        if (currentFrame == null || previousFrame == null)
        {
            return ResultEx.Ok(Rectangle.Empty);
        }

        if (forceFullscreen)
        {
            return ResultEx.Ok(new Rectangle(new Point(0, 0), currentFrame.Size));
        }

        if (currentFrame.Height != previousFrame.Height || currentFrame.Width != previousFrame.Width)
        {
            return ResultEx.Fail<Rectangle>("Bitmaps are not of equal dimensions.");
        }

        if (currentFrame.PixelFormat != previousFrame.PixelFormat)
        {
            return ResultEx.Fail<Rectangle>("Bitmaps are not the same format.");
        }

        var width = currentFrame.Width;
        var height = currentFrame.Height;
        int left = int.MaxValue;
        int top = int.MaxValue;
        int right = int.MinValue;
        int bottom = int.MinValue;

        BitmapData bd1 = new();
        BitmapData bd2 = new();

        try
        {
            bd1 = previousFrame.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, currentFrame.PixelFormat);
            bd2 = currentFrame.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, previousFrame.PixelFormat);

            var bytesPerPixel = Image.GetPixelFormatSize(currentFrame.PixelFormat) / 8;
            var totalSize = bd1.Height * bd1.Width * bytesPerPixel;

            unsafe
            {
                byte* scan1 = (byte*)bd1.Scan0.ToPointer();
                byte* scan2 = (byte*)bd2.Scan0.ToPointer();

                for (var row = 0; row < height; row++)
                {
                    for (var column = 0; column < width; column++)
                    {
                        var index = row * width * bytesPerPixel + column * bytesPerPixel;

                        byte* data1 = scan1 + index;
                        byte* data2 = scan2 + index;

                        if (data1[0] != data2[0] ||
                            data1[1] != data2[1] ||
                            data1[2] != data2[2])
                        {

                            if (row < top)
                            {
                                top = row;
                            }
                            if (row > bottom)
                            {
                                bottom = row;
                            }
                            if (column < left)
                            {
                                left = column;
                            }
                            if (column > right)
                            {
                                right = column;
                            }
                        }

                    }
                }

                if (left <= right && top <= bottom)
                {
                    left = Math.Max(left - 2, 0);
                    top = Math.Max(top - 2, 0);
                    right = Math.Min(right + 2, width);
                    bottom = Math.Min(bottom + 2, height);

                    return ResultEx.Ok(new Rectangle(left, top, right - left, bottom - top));
                }
                else
                {
                    return ResultEx.Ok(Rectangle.Empty);
                }
            }
        }
        catch (Exception ex)
        {
            return ResultEx.Fail<Rectangle>(ex);
        }
        finally
        {
            try
            {
                currentFrame.UnlockBits(bd1);
                previousFrame.UnlockBits(bd2);
            }
            catch { }
        }
    }

    public bool IsEmpty(Bitmap bitmap)
    {
        var bounds = new Rectangle(Point.Empty, bitmap.Size);
        var height = bounds.Height;
        var width = bounds.Width;

        BitmapData? bd = null;

        try
        {
            bd = bitmap.LockBits(bounds, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

            var bytesPerPixel = Image.GetPixelFormatSize(bitmap.PixelFormat) / 8;
            var totalSize = bd.Height * bd.Width * bytesPerPixel;

            unsafe
            {
                byte* scan = (byte*)bd.Scan0.ToPointer();

                for (var row = 0; row < height; row++)
                {
                    for (var column = 0; column < width; column++)
                    {
                        var index = row * width * bytesPerPixel + column * bytesPerPixel;

                        byte* data = scan + index;

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
        finally
        {
            try
            {
                if (bd is not null)
                {
                    bitmap.UnlockBits(bd);
                }
            }
            catch { }
        }
    }
}
