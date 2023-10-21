namespace ControlR.Shared.Dtos;

public enum DtoType
{
    None = 0,
    PublicKey = 1,
    VncSessionRequest = 2,
    WindowsSessions = 3,
    DeviceUpdateRequest = 4,
    RtcSessionDescription = 5,
    RtcIceCandidate = 6,
    CloseStreamingSession = 7,
    PowerStateChange = 8,
    InvokeCtrlAltDel = 9,
}