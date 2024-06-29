namespace ControlR.Libraries.Shared.Dtos.StreamerDtos;

[MessagePackObject]
public record StreamerInitDataDto(
    [property: MsgPackKey] Guid SessionId,
    [property: MsgPackKey] Uri WebSocketUri,
    [property: MsgPackKey] string StreamerConnectionId,
    [property: MsgPackKey] DisplayDto[] Displays);