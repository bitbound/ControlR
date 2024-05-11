namespace ControlR.Shared.Dtos.SidecarDtos;

public record MouseButtonEventDto(int X, int Y, int Button, bool IsPressed) : SidecarDtoBase(SidecarDtoType.MouseButtonEvent);