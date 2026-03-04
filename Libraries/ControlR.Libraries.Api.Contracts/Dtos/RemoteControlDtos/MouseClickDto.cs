namespace ControlR.Libraries.Api.Contracts.Dtos.RemoteControlDtos;

[MessagePackObject(keyAsPropertyName: true)]
public record MouseClickDto(
    int Button,
    bool IsDoubleClick,
    double PercentX,
    double PercentY);