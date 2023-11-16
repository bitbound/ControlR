namespace ControlR.Shared.Dtos;

public enum DtoType
{
    None = 0,
    PublicKey = 1,
    VncSessionRequest = 2,
    WindowsSessions = 3,
    DeviceUpdateRequest = 4,
    TerminalSessionRequest = 5,
    CloseTerminalRequest = 7,
    PowerStateChange = 8,
    TerminalInput = 9,
}