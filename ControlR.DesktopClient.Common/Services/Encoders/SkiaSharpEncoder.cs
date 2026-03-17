using ControlR.Libraries.Api.Contracts.Enums;
using ControlR.Libraries.Api.Contracts.Dtos.RemoteControlDtos;
using SkiaSharp;

namespace ControlR.DesktopClient.Common.Services.Encoders;

public class SkiaSharpEncoder(IImageUtility imageUtility) : IFrameEncoder
{
    private readonly IImageUtility _imageUtility = imageUtility;
    public CaptureEncoderType Type => CaptureEncoderType.Image;

    public byte[] EncodeFullFrame(SKBitmap frame, int quality, ImageFormat format = ImageFormat.Jpeg)
    {
        return EncodeToMemory(frame, quality, format);
    }

    public byte[] EncodeRegion(SKBitmap frame, SKRect region, int quality, ImageFormat format = ImageFormat.Jpeg)
    {
        using var cropped = _imageUtility.CropBitmap(frame, region);
        return EncodeToMemory(cropped, quality, format);
    }

    private static byte[] EncodeToMemory(SKBitmap bitmap, int quality, ImageFormat format)
    {
        using var ms = new MemoryStream();
        var skFormat = format switch
        {
            ImageFormat.Jpeg => SKEncodedImageFormat.Jpeg,
            ImageFormat.Png => SKEncodedImageFormat.Png,
            ImageFormat.WebP => SKEncodedImageFormat.Webp,
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, null)
        };
        bitmap.Encode(ms, skFormat, quality);
        return ms.ToArray();
    }
}
