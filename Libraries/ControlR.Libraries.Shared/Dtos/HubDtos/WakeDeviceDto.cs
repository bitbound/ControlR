namespace ControlR.Libraries.Shared.Dtos.HubDtos;

[MessagePackObject]
public record WakeDeviceDto(
    [property: MsgPackKey] string[] MacAddresses);