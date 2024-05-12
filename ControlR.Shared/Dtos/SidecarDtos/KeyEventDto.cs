using ControlR.Shared.Enums;

namespace ControlR.Shared.Dtos.SidecarDtos;

public record KeyEventDto(string Key, JsKeyType JsKeyType, bool IsPressed) : SidecarDtoBase(SidecarDtoType.KeyEvent);