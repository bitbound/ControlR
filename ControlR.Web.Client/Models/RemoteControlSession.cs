namespace ControlR.Web.Client.Models;

public class RemoteControlSession(
  DeviceDto device,
  int targetSystemSession,
  int targetProcessId)
{
  public DeviceDto Device { get; } = device;
  public Guid SessionId { get; private set; } = Guid.NewGuid();
  public int TargetProcessId { get; } = targetProcessId;
  public int TargetSystemSession { get; } = targetSystemSession;

  public void CreateNewSessionId()
  {
    SessionId = Guid.NewGuid();
  }
}
