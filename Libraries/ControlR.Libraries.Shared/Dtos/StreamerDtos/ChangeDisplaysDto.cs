namespace ControlR.Libraries.Shared.Dtos.StreamerDtos;

[MessagePackObject]
public record ChangeDisplaysDto([property: Key(0)] string DisplayId);