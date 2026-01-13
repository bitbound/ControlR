namespace ControlR.Libraries.Viewer.Common.State;

public interface IRemoteControlState : IStateBase
{
  double CanvasPixelHeight { get; }
  double CanvasPixelWidth { get; }
  double CanvasScale { get; set; }
  IDisposable? ConnectionClosedRegistration { get; set; }
  RemoteControlSession? CurrentSession { get; set; }
  DisplayDto[]? DisplayData { get; set; }
  bool IsAutoPanEnabled { get; set; }
  bool IsBlockUserInputEnabled { get; set; }
  bool IsMetricsEnabled { get; set; }
  bool IsPrivacyScreenEnabled { get; set; }
  bool IsScrollModeToggled { get; set; }
  bool IsViewOnlyEnabled { get; set; }
  bool IsVirtualKeyboardToggled { get; set; }
  double MaxCanvasScale { get; }
  double MinCanvasScale { get; }
  DisplayDto? SelectedDisplay { get; set; }
  ViewMode ViewMode { get; set; }
}

public class RemoteControlState(ILogger<StateBase> logger) : StateBase(logger), IRemoteControlState
{
  public double CanvasPixelHeight => (SelectedDisplay?.Height ?? 0) * CanvasScale;
  public double CanvasPixelWidth => (SelectedDisplay?.Width ?? 0) * CanvasScale;
  public double CanvasScale
  {
    get => Get(defaultValue: 1.0);
    set => Set(Math.Round(value, 2));
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
  public bool IsBlockUserInputEnabled
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
  public double MaxCanvasScale => 3;
  public double MinCanvasScale => 0.2;
  public DisplayDto? SelectedDisplay
  {
    get => Get<DisplayDto?>();
    set => Set(value);
  }
  public ViewMode ViewMode
  {
    get => Get(defaultValue: ViewMode.Stretch);
    set => Set(value);
  }
}
