namespace ControlR.Web.Client.Models;
public class RemoteControlSession(DeviceViewModel device, int initialSystemSession)
{
  public DeviceViewModel Device { get; } = device;
  public int InitialSystemSession { get; } = initialSystemSession;
  public Guid SessionId { get; private set; } = Guid.NewGuid();

  public void CreateNewSessionId()
  {
    SessionId = Guid.NewGuid();
  }
}
