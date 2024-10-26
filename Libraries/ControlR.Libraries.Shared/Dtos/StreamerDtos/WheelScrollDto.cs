namespace ControlR.Libraries.Shared.Dtos.StreamerDtos;

[MessagePackObject]
public record WheelScrollDto(
    [property: MsgPackKey] double PercentX,
    [property: MsgPackKey] double PercentY,
    [property: MsgPackKey] double ScrollY,
    [property: MsgPackKey] double ScrollX);