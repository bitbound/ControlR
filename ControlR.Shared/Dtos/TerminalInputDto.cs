using ControlR.Shared.Serialization;
using MessagePack;

namespace ControlR.Shared.Dtos;
[MessagePackObject]
public record TerminalInputDto(
    [property: MsgPackKey] Guid TerminalId,
    [property: MsgPackKey] string Input);