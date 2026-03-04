namespace ControlR.Libraries.Api.Contracts.Dtos.RemoteControlDtos;

[MessagePackObject(keyAsPropertyName: true)]
public record CaptureMetricsDto(
  double Fps,
  string CaptureMode,
  Dictionary<string, string> ExtraData);