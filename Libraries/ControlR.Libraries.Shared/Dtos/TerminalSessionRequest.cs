using ControlR.Libraries.Shared.Serialization;
using MessagePack;

namespace ControlR.Libraries.Shared.Dtos;

[MessagePackObject]
public record TerminalSessionRequest(
    [property: MsgPackKey] Guid TerminalId,
    [property: MsgPackKey] string ViewerConnectionId);