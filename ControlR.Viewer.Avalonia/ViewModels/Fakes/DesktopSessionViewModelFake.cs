#pragma warning disable CS0067
namespace ControlR.Viewer.Avalonia.ViewModels.Fakes;
internal class DesktopSessionViewModelFake : IDesktopSessionViewModel
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
  }

  public event EventHandler<DesktopSession>? PreviewRequested;
  public event EventHandler<DesktopSession>? ConnectRequested;

  public bool AreRemoteControlPermissionsGranted => Session.AreRemoteControlPermissionsGranted;
  public string Name => Session.Name;
  public int ProcessId => Session.ProcessId;
  public DesktopSession Session { get; }
  public int SystemSessionId => Session.SystemSessionId;
  public DesktopSessionType Type => Session.Type;
  public string Username => Session.Username;
}
