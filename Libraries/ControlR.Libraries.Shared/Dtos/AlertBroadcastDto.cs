using ControlR.Libraries.Shared.Enums;
using ControlR.Libraries.Shared.Serialization;
using MessagePack;

namespace ControlR.Libraries.Shared.Dtos;

[MessagePackObject]
public record AlertBroadcastDto(
    [property: MsgPackKey] string Message,
    [property: MsgPackKey] AlertSeverity Severity);