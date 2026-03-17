using MessagePack;

namespace ControlR.Libraries.CaptureRecording;

public sealed class CapturePlaybackEvent
{
  public required string EventType { get; init; }
  public required byte[] Payload { get; init; }
  public string PayloadType { get; init; } = string.Empty;
  public required int Sequence { get; init; }
  public required TimeSpan Timestamp { get; init; }

  public T GetPayload<T>()
  {
    return MessagePackSerializer.Deserialize<T>(Payload);
  }
}
