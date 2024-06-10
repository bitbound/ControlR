namespace ControlR.Libraries.Shared.Dtos.SidecarDtos;

public record MouseButtonEventDto(double X, double Y, int Button, bool IsPressed) : SidecarDtoBase(SidecarDtoType.MouseButtonEvent);