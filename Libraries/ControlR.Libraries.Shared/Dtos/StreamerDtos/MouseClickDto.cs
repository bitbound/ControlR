namespace ControlR.Libraries.Shared.Dtos.StreamerDtos;

[MessagePackObject(keyAsPropertyName: true)]
public record MouseClickDto(
    int Button,
    bool IsDoubleClick,
    double PercentX,
    double PercentY);