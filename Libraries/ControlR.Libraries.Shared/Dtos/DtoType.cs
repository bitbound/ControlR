namespace ControlR.Libraries.Shared.Dtos;

public enum DtoType
{
    None = 0,
    IdentityAttestation = 1,
    StreamingSessionRequest = 2,
    WindowsSessions = 3,
    DeviceUpdateRequest = 4,
    TerminalSessionRequest = 5,
    CloseTerminalRequest = 7,
    PowerStateChange = 8,
    TerminalInput = 9,
    StartRdpProxy = 10,
    GetAgentAppSettings = 11,
    SendAppSettings = 12,
    WakeDevice = 13,
    GetAgentCountRequest = 14,
    SendAlertBroadcast = 15,
    ClearAlerts = 16,
    RtcSessionDescription = 17,
    RtcIceCandidate = 18,
    CloseStreamingSession = 19,
    InvokeCtrlAltDel = 20,
    ClipboardChanged = 21,
    AgentUpdateTrigger = 22,
    StreamerInitData = 23,
}