namespace ControlR.Libraries.Shared.Dtos.StreamerDtos;

[MessagePackObject(keyAsPropertyName: true)]
public record MouseButtonEventDto(
    int Button,
    bool IsPressed,
    double PercentX,
    double PercentY);