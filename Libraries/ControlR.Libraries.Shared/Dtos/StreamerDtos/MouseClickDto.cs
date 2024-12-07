namespace ControlR.Libraries.Shared.Dtos.StreamerDtos;

[MessagePackObject]
public record MouseClickDto(
    [property: Key(0)] int Button,
    [property: Key(1)] bool IsDoubleClick,
    [property: Key(2)] double PercentX,
    [property: Key(3)] double PercentY);