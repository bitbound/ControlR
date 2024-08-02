using ControlR.Libraries.Shared.Enums;

namespace ControlR.Libraries.Shared.Dtos;

[MessagePackObject]
public record AlertBroadcastDto(
    [property: MsgPackKey] string Message,
    [property: MsgPackKey] AlertSeverity Severity) : DtoRecordBase;