namespace ControlR.Libraries.Shared.Dtos.RemoteControlDtos;

[MessagePackObject(keyAsPropertyName: true)]
public record CaptureMetricsDto(
  double Fps,
  string CaptureMode,
  Dictionary<string, string> ExtraData);