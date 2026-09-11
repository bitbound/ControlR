namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.UserPreferences;

public sealed record UserPreferencesDto(
  double AutoQualityLowerThresholdMbps,
  int AutoQualityMaximum,
  int AutoQualityMinimum,
  double AutoQualityUpperThresholdMbps,
  bool CaptureCursor,
  ImageFormat EncodingFormat,
  bool EnableDirectX,
  bool HideOfflineDevices,
  bool ShowOnlyUntaggedDevices,
  bool ShowOnlyUngroupedDevices,
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
