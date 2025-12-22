namespace ControlR.Libraries.Shared.Dtos.RemoteControlDtos;

[MessagePackObject(keyAsPropertyName: true)]
public record KeyEventDto(
    string Key,
    string Code,
    bool IsPressed);