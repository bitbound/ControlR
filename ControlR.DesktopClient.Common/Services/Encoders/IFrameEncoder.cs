using System.Buffers;
using ControlR.Libraries.Api.Contracts.Enums;
using ControlR.Libraries.Api.Contracts.Dtos.RemoteControlDtos;
using SkiaSharp;

namespace ControlR.DesktopClient.Common.Services.Encoders;

public interface IFrameEncoder
{
    CaptureEncoderType Type { get; }

    byte[] EncodeFullFrame(SKBitmap frame, int quality, ImageFormat format = ImageFormat.Jpeg);
    byte[] EncodeRegion(SKBitmap frame, SKRect region, int quality, ImageFormat format = ImageFormat.Jpeg);
}
