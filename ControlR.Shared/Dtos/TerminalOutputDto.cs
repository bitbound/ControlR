using ControlR.Shared.Enums;
using ControlR.Shared.Serialization;
using MessagePack;

namespace ControlR.Shared.Dtos;

[MessagePackObject]
public record TerminalOutputDto(
    [property: MsgPackKey] Guid TerminalId,
    [property: MsgPackKey] string Output,
    [property: MsgPackKey] TerminalOutputKind OutputKind,
    [property: MsgPackKey] DateTimeOffset Timestamp);