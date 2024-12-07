namespace ControlR.Libraries.Shared.Dtos.HubDtos;

[MessagePackObject]
public record CloseTerminalRequestDto([property: Key(0)] Guid TerminalId);