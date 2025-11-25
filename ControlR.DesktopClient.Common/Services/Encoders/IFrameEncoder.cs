using System.Buffers;
using ControlR.Libraries.Shared.Enums;
using SkiaSharp;

namespace ControlR.DesktopClient.Common.Services.Encoders;

public interface IFrameEncoder
{
    CaptureEncoderType Type { get; }
    byte[] EncodeRegion(SKBitmap frame, SKRect region, int quality);
    byte[] EncodeFullFrame(SKBitmap frame, int quality);
}
