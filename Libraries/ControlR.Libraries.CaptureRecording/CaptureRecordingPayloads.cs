using ControlR.Libraries.Api.Contracts.Dtos.RemoteControlDtos;
using MessagePack;

namespace ControlR.Libraries.CaptureRecording;

[MessagePackObject(keyAsPropertyName: true, AllowPrivate = true)]
internal sealed class CaptureEventRecordData
{
  public required string EventType { get; init; }
  public required byte[] Payload { get; init; }
  public string PayloadType { get; init; } = string.Empty;
}

[MessagePackObject(keyAsPropertyName: true, AllowPrivate = true)]
internal sealed class CaptureFrameRecordData
{
  public string CaptureMode { get; init; } = string.Empty;
  public required ScreenRegionDto[] Regions { get; init; }
}

[MessagePackObject(keyAsPropertyName: true, AllowPrivate = true)]
internal sealed class CaptureKeyFrameRecordData
{
  public string CaptureMode { get; init; } = string.Empty;
  public required byte[] EncodedImage { get; init; }
  public required ImageFormat ImageFormat { get; init; }
}
