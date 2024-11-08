namespace ControlR.Libraries.Shared.Dtos.HubDtos;

[MessagePackObject]
public record TerminalSessionRequest(
    [property: MsgPackKey] Guid TerminalId,
    [property: MsgPackKey] string ViewerConnectionId);