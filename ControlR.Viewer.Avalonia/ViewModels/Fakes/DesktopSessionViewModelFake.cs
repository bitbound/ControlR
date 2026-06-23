#pragma warning disable CS0067
using CommunityToolkit.Mvvm.Input;

namespace ControlR.Viewer.Avalonia.ViewModels.Fakes;
internal class DesktopSessionViewModelFake : IRemoteControlDesktopCardViewModel
{
  public DesktopSessionViewModelFake()
  {
    Session = new DesktopSession
    {
      Name = "Console Session",
      Username = "demo.user",
      SystemSessionId = 1,
      ProcessId = 9999,
      Type = DesktopSessionType.Console,
      AreRemoteControlPermissionsGranted = true
    };

    PreviewCommand = new RelayCommand(() => PreviewRequested?.Invoke(this, Session));
    ConnectCommand = new RelayCommand(() => ConnectRequested?.Invoke(this, Session));
    RequestPermissionsCommand = new RelayCommand(() => RemoteControlPermissionRequested?.Invoke(this, Session));
  }

  public event EventHandler<DesktopSession>? PreviewRequested;
  public event EventHandler<DesktopSession>? ConnectRequested;
  public event EventHandler<DesktopSession>? RemoteControlPermissionRequested;

  public bool AreRemoteControlPermissionsGranted => Session.AreRemoteControlPermissionsGranted;
  public IRelayCommand ConnectCommand { get; }
  public string Name => Session.Name;
  public IRelayCommand PreviewCommand { get; }
  public int ProcessId => Session.ProcessId;
  public IRelayCommand RequestPermissionsCommand { get; }
  public DesktopSession Session { get; }
  public int SystemSessionId => Session.SystemSessionId;
  public DesktopSessionType Type => Session.Type;
  public string Username => Session.Username;
}
