namespace ControlR.Libraries.Viewer.Common.State;

public interface IRemoteControlState : IStateBase
{
  IDisposable? ConnectionClosedRegistration { get; set; }
  RemoteControlSession? CurrentSession { get; set; }
  DisplayDto[]? DisplayData { get; set; }
  bool IsMetricsEnabled { get; set; }
  bool IsScrollModeToggled { get; set; }
  bool IsViewOnlyEnabled { get; set; }
  bool IsVirtualKeyboardToggled { get; set; }
  DisplayDto? SelectedDisplay { get; set; }
  ViewMode ViewMode { get; set; }
}

public class RemoteControlState(ILogger<StateBase> logger) : StateBase(logger), IRemoteControlState
{
  public IDisposable? ConnectionClosedRegistration { get; set; }
  public RemoteControlSession? CurrentSession { get; set; }
  public DisplayDto[]? DisplayData { get; set; }
  public bool IsMetricsEnabled { get; set; }
  public bool IsScrollModeToggled { get; set; }
  public bool IsViewOnlyEnabled { get; set; }
  public bool IsVirtualKeyboardToggled { get; set; }
  public DisplayDto? SelectedDisplay { get; set; }
  public ViewMode ViewMode { get; set; } = ViewMode.Stretch;
}
