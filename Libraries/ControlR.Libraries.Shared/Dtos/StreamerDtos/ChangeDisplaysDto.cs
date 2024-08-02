namespace ControlR.Libraries.Shared.Dtos.StreamerDtos;

[MessagePackObject]
public record ChangeDisplaysDto([property: MsgPackKey] string DisplayId) : DtoRecordBase;