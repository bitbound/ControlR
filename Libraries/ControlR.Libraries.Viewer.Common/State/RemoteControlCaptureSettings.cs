namespace ControlR.Libraries.Viewer.Common.State;

/// <summary>
/// Maps capture settings between the saved user preferences, the shared viewer state, and the DTO
/// the agent receives. Every page that starts a viewer session does this the same way, so the
/// mapping belongs beside the state instead of copied into each page.
/// </summary>
public static class RemoteControlCaptureSettings
{
  public static void ApplyPreferences(this IRemoteControlState state, UserPreferencesDto preferences)
  {
    state.CaptureCursor = preferences.CaptureCursor;
    state.EnableDirectX = preferences.EnableDirectX;
    state.EncodingFormat = preferences.EncodingFormat;
    state.IsAutoQualityEnabled = preferences.IsAutoQualityEnabled;
    state.ManualQuality = preferences.ManualQuality;
    state.AutoQualityLowerThresholdMbps = preferences.AutoQualityLowerThresholdMbps;
    state.AutoQualityMaximum = preferences.AutoQualityMaximum;
    state.AutoQualityMinimum = preferences.AutoQualityMinimum;
    state.AutoQualityUpperThresholdMbps = preferences.AutoQualityUpperThresholdMbps;
    state.IsMaxBandwidthEnabled = preferences.IsMaxBandwidthEnabled;
    state.MaxBandwidthMbps = preferences.MaxBandwidthMbps;
  }

  public static UpdateCaptureSettingsDto ToCaptureSettingsDto(this IRemoteControlState state) =>
    new(
      state.CaptureCursor,
      state.EnableDirectX,
      state.IsAutoQualityEnabled,
      state.ManualQuality,
      state.AutoQualityLowerThresholdMbps,
      state.AutoQualityMaximum,
      state.AutoQualityMinimum,
      state.AutoQualityUpperThresholdMbps,
      state.IsMaxBandwidthEnabled,
      state.MaxBandwidthMbps,
      state.EncodingFormat);
}
