using ControlR.Libraries.Api.Contracts.Enums;

namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi;

public sealed record UserPreferencesDto(
  double AutoQualityLowerThresholdMbps,
  int AutoQualityMaximum,
  int AutoQualityMinimum,
  double AutoQualityUpperThresholdMbps,
  bool CaptureCursor,
  bool HideOfflineDevices,
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