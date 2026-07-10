namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V0;

public class DeviceSearchResponseDto
{
    public bool AnyDevicesForUser { get; set; }
    public DeviceSearchFilterCountsDto FilterCounts { get; set; } = new();
    public int HiddenUntaggedDevices { get; set; }
    public List<DeviceResponseDto>? Items { get; set; }
    public int TotalItems { get; set; }
}
