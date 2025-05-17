namespace ControlR.Libraries.Shared.Dtos.StreamerDtos;

[MessagePackObject(keyAsPropertyName: true)]
public record KeyEventDto(
    string Key,
    bool IsPressed);