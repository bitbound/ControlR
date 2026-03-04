using ControlR.Libraries.Api.Contracts.Enums;

namespace ControlR.Libraries.Api.Contracts.Dtos.RemoteControlDtos;

[MessagePackObject(keyAsPropertyName: true)]
public record KeyEventDto(
    string Key,
    string Code,
    bool IsPressed,
    KeyboardInputMode InputMode,
    KeyEventModifiersDto Modifiers);