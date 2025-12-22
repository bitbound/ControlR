namespace ControlR.Libraries.Shared.Dtos.StreamerDtos;

[MessagePackObject(keyAsPropertyName: true)]
public record CaptureMetricsDto(
  double Mbps,
  double Fps,
  bool IsUsingGpu,
  DateTimeOffset Timestamp,
  Dictionary<string, string> ExtraData);