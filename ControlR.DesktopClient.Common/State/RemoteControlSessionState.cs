using ControlR.Libraries.Shared.Constants;
using ControlR.Libraries.Shared.Services.StateManagement;
using Microsoft.Extensions.Logging;

namespace ControlR.DesktopClient.Common.State;

internal interface IRemoteControlSessionState : IStateBase
{
  double AutoQualityLowerThresholdMbps { get; set; }
  int AutoQualityMaximum { get; set; }
  int AutoQualityMinimum { get; set; }
  double AutoQualityUpperThresholdMbps { get; set; }
  bool CaptureCursor { get; set; }
  int ImageQuality { get; set; }
  bool IsAutoQualityEnabled { get; set; }
  bool IsMaxBandwidthEnabled { get; set; }
  double MaxBandwidthMbps { get; set; }
}

internal class RemoteControlSessionState(ILogger<RemoteControlSessionState> logger)
  : ObservableState(logger), IRemoteControlSessionState
{
  public double AutoQualityLowerThresholdMbps
  {
    get => Get(defaultValue: AppConstants.DefaultRemoteControlAutoQualityLowerThresholdMbps);
    set
    {
      var normalized = Math.Max(0.1d, Math.Round(value, 2));
      Set(normalized);

      if (AutoQualityUpperThresholdMbps <= normalized)
      {
        AutoQualityUpperThresholdMbps = normalized + 0.1d;
      }
    }
  }
  public int AutoQualityMaximum
  {
    get => Get(defaultValue: AppConstants.DefaultRemoteControlAutoQualityMaximum);
    set => Set(Math.Clamp(value, AutoQualityMinimum + 1, 100));
  }
  public int AutoQualityMinimum
  {
    get => Get(defaultValue: AppConstants.DefaultRemoteControlAutoQualityMinimum);
    set
    {
      var normalized = Math.Clamp(value, 1, 99);
      Set(normalized);

      if (AutoQualityMaximum <= normalized)
      {
        AutoQualityMaximum = normalized + 1;
      }
    }
  }
  public double AutoQualityUpperThresholdMbps
  {
    get => Get(defaultValue: AppConstants.DefaultRemoteControlAutoQualityUpperThresholdMbps);
    set => Set(Math.Max(AutoQualityLowerThresholdMbps + 0.1d, Math.Round(value, 2)));
  }
  public bool CaptureCursor
  {
    get => Get(defaultValue: AppConstants.DefaultRemoteControlCaptureCursor);
    set => Set(value);
  }
  public int ImageQuality
  {
    get => Get(defaultValue: AppConstants.DefaultRemoteControlManualQuality);
    set => Set(Math.Clamp(value, 1, 100));
  }
  public bool IsAutoQualityEnabled
  {
    get => Get(defaultValue: AppConstants.DefaultRemoteControlIsAutoQualityEnabled);
    set => Set(value);
  }
  public bool IsMaxBandwidthEnabled
  {
    get => Get(defaultValue: AppConstants.DefaultRemoteControlIsMaxBandwidthEnabled);
    set => Set(value);
  }
  public double MaxBandwidthMbps
  {
    get => Get(defaultValue: AppConstants.DefaultRemoteControlMaxBandwidthMbps);
    set => Set(Math.Max(0.1d, Math.Round(value, 2)));
  }
}