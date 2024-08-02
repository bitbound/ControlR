namespace ControlR.Libraries.Shared.Dtos.SidecarDtos;

[MessagePackObject]
public record MouseClickDto(
    [property: MsgPackKey] int Button,
    [property: MsgPackKey] bool IsDoubleClick,
    [property: MsgPackKey] double PercentX,
    [property: MsgPackKey] double PercentY) : DtoRecordBase;