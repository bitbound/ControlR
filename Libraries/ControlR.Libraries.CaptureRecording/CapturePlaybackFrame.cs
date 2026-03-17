using SkiaSharp;

namespace ControlR.Libraries.CaptureRecording;

public sealed class CapturePlaybackFrame : IDisposable
{
  public required SKBitmap Image { get; init; }
  public required int Sequence { get; init; }
  public required TimeSpan Timestamp { get; init; }

  public void Dispose()
  {
    Image.Dispose();
  }
}
