namespace ControlR.Libraries.Shared.Dtos.SidecarDtos;

[MessagePackObject]
public record DesktopChangedDto([property: MsgPackKey] string DesktopName) : DtoRecordBase;
