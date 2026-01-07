namespace ControlR.Libraries.Viewer.Common.Models;

public class RemoteControlSession(
  DeviceResponseDto device,
  int targetSystemSession,
  int targetProcessId,
  DesktopSessionType desktopSessionType)
{
  public DesktopSessionType DesktopSessionType { get; } = desktopSessionType;
  public DeviceResponseDto Device { get; } = device;
  public Guid SessionId { get; private set; } = Guid.NewGuid();
  public int TargetProcessId { get; } = targetProcessId;
  public int TargetSystemSession { get; } = targetSystemSession;

  public void CreateNewSessionId()
  {
    SessionId = Guid.NewGuid();
  }
}
