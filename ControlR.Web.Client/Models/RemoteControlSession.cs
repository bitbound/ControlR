namespace ControlR.Web.Client.Models;
public class RemoteControlSession(DeviceDto deviceDto, int initialSystemSession)
{
  public DeviceDto DeviceDto { get; } = deviceDto;
  public int InitialSystemSession { get; } = initialSystemSession;
  public Guid SessionId { get; private set; } = Guid.NewGuid();

  public void CreateNewSessionId()
    {
        SessionId = Guid.NewGuid();
    }
}
