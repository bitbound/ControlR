namespace ControlR.Libraries.Shared.Dtos;

[MessagePackObject]
public record CloseTerminalRequestDto([property: MsgPackKey] Guid TerminalId) : ParameterlessDtoBase(DtoType.CloseTerminalRequest);