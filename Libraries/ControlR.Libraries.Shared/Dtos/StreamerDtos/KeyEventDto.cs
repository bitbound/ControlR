namespace ControlR.Libraries.Shared.Dtos.StreamerDtos;

[MessagePackObject]
public record KeyEventDto(
    [property: MsgPackKey] string Key,
    [property: MsgPackKey] bool IsPressed);