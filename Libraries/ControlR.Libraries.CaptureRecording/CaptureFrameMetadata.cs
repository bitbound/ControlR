namespace ControlR.Libraries.CaptureRecording;

public sealed class CaptureFrameMetadata
{
  public required int CanvasHeight { get; init; }
  public required int CanvasWidth { get; init; }
  public string CaptureMode { get; init; } = string.Empty;
  public bool IsKeyFrame { get; init; }
  public TimeSpan? Timestamp { get; init; }
}
