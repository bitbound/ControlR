using ControlR.Libraries.Shared.Enums;
using ControlR.Libraries.Shared.Serialization;
using MessagePack;

namespace ControlR.Libraries.Shared.Dtos;

[MessagePackObject]
public record TerminalOutputDto(
    [property: MsgPackKey] Guid TerminalId,
    [property: MsgPackKey] string Output,
    [property: MsgPackKey] TerminalOutputKind OutputKind,
    [property: MsgPackKey] DateTimeOffset Timestamp);