namespace ControlR.Libraries.Api.Contracts.Dtos.RemoteControlDtos;

[MessagePackObject(keyAsPropertyName: true)]
public record MouseButtonEventDto(
    int Button,
    bool IsPressed,
    double NormalizedX,
    double NormalizedY);