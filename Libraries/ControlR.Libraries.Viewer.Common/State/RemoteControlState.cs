namespace ControlR.Libraries.Viewer.Common.State;

public interface IRemoteControlState : IStateBase
{
  IDisposable? ConnectionClosedRegistration { get; set; }
  RemoteControlSession? CurrentSession { get; set; }
  DisplayDto[]? DisplayData { get; set; }
  bool IsBlockUserInputEnabled { get; set; }
  bool IsMetricsEnabled { get; set; }
  bool IsPrivacyScreenEnabled { get; set; }
  bool IsScrollModeToggled { get; set; }
  bool IsViewOnlyEnabled { get; set; }
  bool IsVirtualKeyboardToggled { get; set; }
  DisplayDto? SelectedDisplay { get; set; }
  ViewMode ViewMode { get; set; }
}

public class RemoteControlState(ILogger<StateBase> logger) : StateBase(logger), IRemoteControlState
{
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
