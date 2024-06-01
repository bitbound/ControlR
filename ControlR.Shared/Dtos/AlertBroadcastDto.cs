using ControlR.Shared.Enums;
using ControlR.Shared.Serialization;
using MessagePack;

namespace ControlR.Shared.Dtos;

[MessagePackObject]
public record AlertBroadcastDto(
    [property: MsgPackKey] string Message,
    [property: MsgPackKey] AlertSeverity Severity);