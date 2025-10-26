namespace ControlR.Libraries.Shared.Dtos.ServerApi;

public class DeviceSearchResponseDto
{
    public bool AnyDevicesForUser { get; set; }
    public List<DeviceDto>? Items { get; set; }
    public int TotalItems { get; set; }
}
