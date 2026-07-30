namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

[MessagePackObject(keyAsPropertyName: true)]
public sealed record UserPreferencesDto(
  double AutoQualityLowerThresholdMbps,
  int AutoQualityMaximum,
  int AutoQualityMinimum,
  double AutoQualityUpperThresholdMbps,
  bool CaptureCursor,
  ImageFormat EncodingFormat,
  bool EnableDirectX,
  bool HideOfflineDevices,
  bool IncludeUntaggedDevices,
  bool IsAutoQualityEnabled,
  bool IsMaxBandwidthEnabled,
  KeyboardInputMode KeyboardInputMode,
  int ManualQuality,
  double MaxBandwidthMbps,
  bool NotifyUserOnSessionStart,
  bool OpenDeviceInNewTab,
  ThemeMode ThemeMode,
  string UserDisplayName,
  ViewMode ViewMode);