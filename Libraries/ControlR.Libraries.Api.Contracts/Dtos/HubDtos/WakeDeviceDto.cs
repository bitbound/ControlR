namespace ControlR.Libraries.Api.Contracts.Dtos.HubDtos;

[MessagePackObject]
public record WakeDeviceDto(
    [property: Key(0)] string[] MacAddresses);