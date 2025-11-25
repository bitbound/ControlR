using ControlR.Libraries.Shared.Enums;
using SkiaSharp;

namespace ControlR.DesktopClient.Common.Services.Encoders;

public class JpegEncoder(IImageUtility imageUtility) : IFrameEncoder
{
    private readonly IImageUtility _imageUtility = imageUtility;
    public CaptureEncoderType Type => CaptureEncoderType.Jpeg;

    public byte[] EncodeFullFrame(SKBitmap frame, int quality)
    {
        return EncodeToMemory(frame, quality);
    }

    public byte[] EncodeRegion(SKBitmap frame, SKRect region, int quality)
    {
        using var cropped = _imageUtility.CropBitmap(frame, region);
        return EncodeToMemory(cropped, quality);
    }

    private byte[] EncodeToMemory(SKBitmap bitmap, int quality)
    {
        using var ms = new MemoryStream();
        bitmap.Encode(ms, SKEncodedImageFormat.Jpeg, quality);
        return ms.ToArray();
    }
}
