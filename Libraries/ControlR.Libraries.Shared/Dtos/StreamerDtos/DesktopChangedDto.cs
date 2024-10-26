namespace ControlR.Libraries.Shared.Dtos.StreamerDtos;

[MessagePackObject]
public record DesktopChangedDto([property: MsgPackKey] string DesktopName);
