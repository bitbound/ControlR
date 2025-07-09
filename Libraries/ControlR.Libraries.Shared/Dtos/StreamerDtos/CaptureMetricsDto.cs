namespace ControlR.Libraries.Shared.Dtos.StreamerDtos;

[MessagePackObject(keyAsPropertyName: true)]
public record CaptureMetricsDto(
  double Mbps,
  double Fps,
  double Ips,
  bool IsUsingGpu,
  int ImageQuality,
  TimeSpan Latency,
  Dictionary<string, string> ExtraData);