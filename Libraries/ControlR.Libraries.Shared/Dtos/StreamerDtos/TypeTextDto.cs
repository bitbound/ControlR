namespace ControlR.Libraries.Shared.Dtos.SidecarDtos;

[MessagePackObject]
public record TypeTextDto([property: MsgPackKey] string Text) : DtoRecordBase;