using ControlR.Libraries.Shared.Enums;

namespace ControlR.Libraries.Shared.Dtos.SidecarDtos;

public record KeyEventDto(string Key, bool IsPressed) : SidecarDtoBase(SidecarDtoType.KeyEvent);