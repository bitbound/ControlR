namespace ControlR.Web.Client.Models;
public class RemoteControlSession(DeviceDto device, int initialSystemSession)
{
    public DeviceDto Device { get; } = device;
    public int InitialSystemSession { get; } = initialSystemSession;
    public Guid SessionId { get; private set; } = Guid.NewGuid();
    public Uri? WebSocketUri { get; internal set; }

    public void CreateNewSessionId()
    {
        SessionId = Guid.NewGuid();
    }
}
