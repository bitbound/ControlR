using ControlR.Libraries.Shared.Services.StateManagement;

namespace ControlR.Libraries.Viewer.Common.State;

public interface IRemoteControlState : IStateBase
{
  double AutoQualityLowerThresholdMbps { get; set; }
  int AutoQualityMaximum { get; set; }
  int AutoQualityMinimum { get; set; }
  double AutoQualityUpperThresholdMbps { get; set; }
  bool CaptureCursor { get; set; }
  IDisposable? ConnectionClosedRegistration { get; set; }
  RemoteControlSession? CurrentSession { get; set; }
  DisplayDto[]? DisplayData { get; set; }
  bool IsAutoPanEnabled { get; set; }
  bool IsAutoQualityEnabled { get; set; }
  bool IsBlockUserInputEnabled { get; set; }
  bool IsMaxBandwidthEnabled { get; set; }
  bool IsMetricsEnabled { get; set; }
  bool IsPrivacyScreenEnabled { get; set; }
  bool IsScrollModeToggled { get; set; }
  bool IsViewOnlyEnabled { get; set; }
  bool IsVirtualKeyboardToggled { get; set; }
  KeyboardInputMode KeyboardInputMode { get; set; }
  int ManualQuality { get; set; }
  double MaxBandwidthMbps { get; set; }
  double MaxRendererScale { get; }
  double MinRendererScale { get; }
  double RendererPixelHeight { get; }
  double RendererPixelWidth { get; }
  double RendererScale { get; set; }
  DisplayDto? SelectedDisplay { get; set; }
  ViewMode ViewMode { get; set; }
}

public class RemoteControlState(ILogger<ObservableState> logger) : ObservableState(logger), IRemoteControlState
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
    get => Get<bool>();
    set => Set(value);
  }
  public IDisposable? ConnectionClosedRegistration
  {
    get => Get<IDisposable?>();
    set => Set(value);
  }
  public RemoteControlSession? CurrentSession
  {
    get => Get<RemoteControlSession?>();
    set => Set(value);
  }
  public DisplayDto[]? DisplayData
  {
    get => Get<DisplayDto[]?>();
    set => Set(value);
  }
  public bool IsAutoPanEnabled
  {
    get => Get(defaultValue: true);
    set => Set(value);
  }
  public bool IsAutoQualityEnabled
  {
    get => Get<bool>();
    set => Set(value);
  }
  public bool IsBlockUserInputEnabled
  {
    get => Get<bool>();
    set => Set(value);
  }
  public bool IsMaxBandwidthEnabled
  {
    get => Get<bool>();
    set => Set(value);
  }
  public bool IsMetricsEnabled
  {
    get => Get<bool>();
    set => Set(value);
  }
  public bool IsPrivacyScreenEnabled
  {
    get => Get<bool>();
    set => Set(value);
  }
  public bool IsScrollModeToggled
  {
    get => Get<bool>();
    set => Set(value);
  }
  public bool IsViewOnlyEnabled
  {
    get => Get<bool>();
    set => Set(value);
  }
  public bool IsVirtualKeyboardToggled
  {
    get => Get<bool>();
    set => Set(value);
  }
  public KeyboardInputMode KeyboardInputMode
  {
    get => Get(defaultValue: KeyboardInputMode.Auto);
    set => Set(value);
  }
  public int ManualQuality
  {
    get => Get(defaultValue: AppConstants.DefaultRemoteControlManualQuality);
    set => Set(Math.Clamp(value, 1, 100));
  }
  public double MaxBandwidthMbps
  {
    get => Get(defaultValue: AppConstants.DefaultRemoteControlMaxBandwidthMbps);
    set => Set(Math.Max(0.1d, Math.Round(value, 2)));
  }
  public double MaxRendererScale => 3;
  public double MinRendererScale => 0.2;
  public double RendererPixelHeight => (SelectedDisplay?.CapturePixelSize.Height ?? 0) * RendererScale;
  public double RendererPixelWidth => (SelectedDisplay?.CapturePixelSize.Width ?? 0) * RendererScale;
  public double RendererScale
  {
    get => Get(defaultValue: 1.0);
    set => Set(Math.Round(value, 2));
  }
  public DisplayDto? SelectedDisplay
  {
    get => Get<DisplayDto?>();
    set => Set(value);
  }
  public SignalingState SignalingState
  {
    get => Get<SignalingState>();
    set => Set(value);
  }
  public ViewMode ViewMode
  {
    get => Get(defaultValue: ViewMode.Fit);
    set => Set(value);
  }
}
