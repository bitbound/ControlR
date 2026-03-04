namespace ControlR.Libraries.Api.Contracts.Dtos.RemoteControlDtos;

[MessagePackObject(keyAsPropertyName: true)]
public record WheelScrollDto(
    double PercentX,
    double PercentY,
    double ScrollY,
    double ScrollX);