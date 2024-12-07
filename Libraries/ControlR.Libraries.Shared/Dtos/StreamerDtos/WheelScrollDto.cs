namespace ControlR.Libraries.Shared.Dtos.StreamerDtos;

[MessagePackObject]
public record WheelScrollDto(
    [property: Key(0)] double PercentX,
    [property: Key(1)] double PercentY,
    [property: Key(2)] double ScrollY,
    [property: Key(3)] double ScrollX);