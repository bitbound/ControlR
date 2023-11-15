using ControlR.Shared.Serialization;
using MessagePack;

namespace ControlR.Shared.Dtos;

[MessagePackObject]
public record TerminalSessionRequest(
    [property: MsgPackKey] Guid TerminalId,
    [property: MsgPackKey] string ViewerConnectionId);