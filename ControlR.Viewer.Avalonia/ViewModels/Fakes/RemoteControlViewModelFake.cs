using ControlR.Libraries.Avalonia.Controls.Snackbar;

namespace ControlR.Viewer.Avalonia.ViewModels.Fakes;

internal class RemoteControlViewModelFake : ViewModelBaseFake<RemoteControlView>, IRemoteControlViewModel
{
  public RemoteControlViewModelFake()
  {
    CurrentState = SignalingState.SessionSelect;
    DesktopSessions =
    [
      new DesktopSessionViewModel(new DesktopSession
      {
        Name = "Desktop 1",
        Username = "john.doe",
        SystemSessionId = 1,
        ProcessId = 1234,
        Type = DesktopSessionType.Console,
        AreRemoteControlPermissionsGranted = true
      }),
      new DesktopSessionViewModel(new DesktopSession
      {
        Name = "Desktop 2",
        Username = "jane.smith",
        SystemSessionId = 2,
        ProcessId = 5678,
        Type = DesktopSessionType.Rdp,
        AreRemoteControlPermissionsGranted = true
      })
    ];
  }

  public string? AlertMessage { get; set; } = "This is an alert message.";
  public SnackbarSeverity AlertSeverity => SnackbarSeverity.Info;
  public SignalingState CurrentState { get; set; }
  public ObservableCollection<DesktopSessionViewModel> DesktopSessions { get; }
  public string DesktopSessionTitle { get; } = "Desktop Session on VMHOST";
  public IAsyncRelayCommand DisconnectCommand { get; } = new AsyncRelayCommand(() => Task.CompletedTask);
  public bool HasDesktopSessions => DesktopSessions.Count > 0;
  public bool IsReconnecting { get; set; }
  public bool IsRemoteDisplayVisible => CurrentState == SignalingState.ConnectionActive;
  public bool IsViewOnlyEnabled { get; set; }
  public string? LoadingMessage { get; set; } = "Loading...";
  public IAsyncRelayCommand RefreshSessionsCommand { get; } = new AsyncRelayCommand(() => Task.CompletedTask);
  public IRemoteDisplayViewModel RemoteDisplayViewModel { get; } = new RemoteDisplayViewModelFake();
}
