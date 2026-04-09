namespace ControlR.Libraries.Api.Contracts.Dtos.RemoteControlDtos;

[MessagePackObject(keyAsPropertyName: true)]
public record CaptureMetricsDto(
  double Fps,
  string CaptureMode,
  int CurrentQuality,
  Dictionary<string, string> ExtraData);