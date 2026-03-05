namespace ControlR.Libraries.Api.Contracts.Dtos.RemoteControlDtos;

[MessagePackObject(keyAsPropertyName: true)]
public record WheelScrollDto(
    double NormalizedX,
    double NormalizedY,
    double ScrollY,
    double ScrollX);