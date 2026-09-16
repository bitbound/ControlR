using V1UserPreferencesDto = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.UserPreferences.UserPreferencesDto;

namespace ControlR.Web.Client.Extensions;

public static class V1DtoToInternalDtoExtensions
{
  /// <summary>
  /// Translates the V1 wire shape into the internal DTO the rest of the client works in, so the
  /// stable contract stops at the API boundary instead of leaking into components and view models.
  /// </summary>
  public static InternalDtos.UserPreferencesDto ToInternalDto(this V1UserPreferencesDto preferences)
  {
    return new InternalDtos.UserPreferencesDto(
      preferences.AutoQualityLowerThresholdMbps,
      preferences.AutoQualityMaximum,
      preferences.AutoQualityMinimum,
      preferences.AutoQualityUpperThresholdMbps,
      preferences.CaptureCursor,
      preferences.EncodingFormat,
      preferences.EnableDirectX,
      preferences.HideOfflineDevices,
      preferences.ShowOnlyUntaggedDevices,
      preferences.ShowOnlyUngroupedDevices,
      preferences.IsAutoQualityEnabled,
      preferences.IsMaxBandwidthEnabled,
      preferences.KeyboardInputMode,
      preferences.ManualQuality,
      preferences.MaxBandwidthMbps,
      preferences.NotifyUserOnSessionStart,
      preferences.OpenDeviceInNewTab,
      preferences.ThemeMode,
      preferences.UserDisplayName,
      preferences.ViewMode);
  }
}