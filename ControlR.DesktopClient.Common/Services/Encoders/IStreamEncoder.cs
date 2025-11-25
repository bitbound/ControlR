using System.Buffers;
using ControlR.Libraries.Shared.Enums;
using SkiaSharp;

namespace ControlR.DesktopClient.Common.Services.Encoders;

public interface IStreamEncoder : IDisposable
{
    CaptureEncoderType Type { get; }
    void Start(int width, int height, int quality);
    void EncodeFrame(SKBitmap frame, bool forceKeyFrame = false);
    byte[]? GetNextPacket(); // Returns null if no data ready
}
