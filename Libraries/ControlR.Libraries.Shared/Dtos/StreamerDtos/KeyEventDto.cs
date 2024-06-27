namespace ControlR.Libraries.Shared.Dtos.SidecarDtos;

[MessagePackObject]
public record KeyEventDto(
    [property: MsgPackKey] string Key, 
    [property: MsgPackKey] bool IsPressed);