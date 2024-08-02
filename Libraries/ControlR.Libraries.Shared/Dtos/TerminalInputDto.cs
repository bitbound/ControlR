namespace ControlR.Libraries.Shared.Dtos;
[MessagePackObject]
public record TerminalInputDto(
    [property: MsgPackKey] Guid TerminalId,
    [property: MsgPackKey] string Input) : DtoRecordBase;