namespace ControlR.Libraries.Shared.Dtos.HubDtos;

[MessagePackObject]
public record WakeDeviceDto(
    [property: Key(0)] string[] MacAddresses);