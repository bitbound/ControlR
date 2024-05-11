namespace ControlR.Shared.Dtos.SidecarDtos;

public record KeyEventDto(string Key, bool IsPressed) : SidecarDtoBase(SidecarDtoType.KeyEvent);