namespace ControlR.Libraries.Shared.Dtos.SidecarDtos;

[MessagePackObject]
public record MouseButtonEventDto(
    [property: MsgPackKey] int Button,
    [property: MsgPackKey] bool IsPressed,
    [property: MsgPackKey] double PercentX,
    [property: MsgPackKey] double PercentY) : DtoRecordBase;