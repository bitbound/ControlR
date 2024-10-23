using ControlR.Libraries.Shared.Enums;

namespace ControlR.Libraries.Shared.Dtos.HubDtos;

[MessagePackObject]
public record TerminalOutputDto(
    [property: MsgPackKey] Guid TerminalId,
    [property: MsgPackKey] string Output,
    [property: MsgPackKey] TerminalOutputKind OutputKind,
    [property: MsgPackKey] DateTimeOffset Timestamp);