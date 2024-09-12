namespace ControlR.Libraries.Shared.Dtos;

[MessagePackObject]
public record WakeDeviceDto(
    [property: MsgPackKey] string[] MacAddresses);