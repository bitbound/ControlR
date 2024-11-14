namespace ControlR.Web.Client.Models;
public class RemoteControlSession(DeviceUpdateResponseDto deviceDto, int initialSystemSession)
{
  public DeviceUpdateResponseDto DeviceDto { get; } = deviceDto;
  public int InitialSystemSession { get; } = initialSystemSession;
  public Guid SessionId { get; private set; } = Guid.NewGuid();

  public void CreateNewSessionId()
    {
        SessionId = Guid.NewGuid();
    }
}
