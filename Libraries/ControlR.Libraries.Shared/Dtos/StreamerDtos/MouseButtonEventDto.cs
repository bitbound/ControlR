namespace ControlR.Libraries.Shared.Dtos.StreamerDtos;

[MessagePackObject]
public record MouseButtonEventDto(
    [property: Key(0)] int Button,
    [property: Key(1)] bool IsPressed,
    [property: Key(2)] double PercentX,
    [property: Key(3)] double PercentY);