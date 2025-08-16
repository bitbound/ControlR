namespace ControlR.Web.Client.Models;

public class RemoteControlSession(
  DeviceViewModel device,
  int targetSystemSession,
  int targetProcessId)
{
  public DeviceViewModel Device { get; } = device;
  public Guid SessionId { get; private set; } = Guid.NewGuid();
  public int TargetProcessId { get; } = targetProcessId;
  public int TargetSystemSession { get; } = targetSystemSession;

  public void CreateNewSessionId()
  {
    SessionId = Guid.NewGuid();
  }
}
