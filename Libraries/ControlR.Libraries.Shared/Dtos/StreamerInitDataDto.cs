namespace ControlR.Libraries.Shared.Dtos;

[MessagePackObject]
public record StreamerInitDataDto(
    [property: MsgPackKey] Guid SessionId,
    [property: MsgPackKey] string StreamerConnectionId,
    [property: MsgPackKey] DisplayDto[] Displays);