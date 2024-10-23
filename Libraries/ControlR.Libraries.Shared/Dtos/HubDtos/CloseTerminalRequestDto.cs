namespace ControlR.Libraries.Shared.Dtos.HubDtos;

[MessagePackObject]
public record CloseTerminalRequestDto([property: MsgPackKey] Guid TerminalId) : ParameterlessDtoBase(DtoType.CloseTerminalRequest);