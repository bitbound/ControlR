namespace ControlR.Libraries.Shared.Dtos.StreamerDtos;

[MessagePackObject]
public record KeyEventDto(
    [property: Key(0)] string Key,
    [property: Key(1)] bool IsPressed);