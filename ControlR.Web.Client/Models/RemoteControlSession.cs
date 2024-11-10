namespace ControlR.Web.Client.Models;
public class RemoteControlSession(DeviceUpdateResponseDto deviceUpdate, int initialSystemSession)
{
    public DeviceUpdateResponseDto DeviceUpdate { get; } = deviceUpdate;
    public int InitialSystemSession { get; } = initialSystemSession;
    public Guid SessionId { get; private set; } = Guid.NewGuid();
    public Uri? WebSocketUri { get; internal set; }

    public void CreateNewSessionId()
    {
        SessionId = Guid.NewGuid();
    }
}
