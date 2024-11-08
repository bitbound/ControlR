namespace ControlR.Libraries.Shared.Dtos.HubDtos;
[MessagePackObject]
public record TerminalInputDto(
    [property: MsgPackKey] Guid TerminalId,
    [property: MsgPackKey] string Input);