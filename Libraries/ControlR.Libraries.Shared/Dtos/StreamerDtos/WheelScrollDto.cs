namespace ControlR.Libraries.Shared.Dtos.StreamerDtos;

[MessagePackObject(keyAsPropertyName: true)]
public record WheelScrollDto(
    double PercentX,
    double PercentY,
    double ScrollY,
    double ScrollX);