using MessagePack;

namespace ControlR.Libraries.Api.Contracts.Dtos.RemoteControlDtos;

[MessagePackObject(keyAsPropertyName: true)]
public record UpdateCaptureSettingsDto(
  bool CaptureCursor,
  bool IsAutoQualityEnabled,
  int ManualQuality,
  double AutoQualityLowerThresholdMbps,
  int AutoQualityMaximum,
  int AutoQualityMinimum,
  double AutoQualityUpperThresholdMbps,
  bool IsMaxBandwidthEnabled,
  double MaxBandwidthMbps);