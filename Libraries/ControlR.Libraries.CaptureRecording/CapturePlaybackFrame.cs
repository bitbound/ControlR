using SkiaSharp;

namespace ControlR.Libraries.CaptureRecording;

/// <summary>
/// Represents a single frame emitted during capture playback.
/// Consumers must dispose each frame when done to release the underlying image bitmap.
/// </summary>
public sealed class CapturePlaybackFrame : IDisposable
{
  /// <summary>
  /// The composited frame image. Owned by this instance and disposed with it.
  /// </summary>
  public required SKBitmap Image { get; init; }
  public required int Sequence { get; init; }
  public required TimeSpan Timestamp { get; init; }

  public void Dispose()
  {
    Image.Dispose();
  }
}
