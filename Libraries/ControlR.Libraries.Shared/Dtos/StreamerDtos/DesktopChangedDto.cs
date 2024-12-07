namespace ControlR.Libraries.Shared.Dtos.StreamerDtos;

[MessagePackObject]
public record DesktopChangedDto([property: Key(0)] string DesktopName);
