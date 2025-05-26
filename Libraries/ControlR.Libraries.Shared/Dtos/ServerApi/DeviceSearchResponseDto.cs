namespace ControlR.Libraries.Shared.Dtos.ServerApi;

public class DeviceSearchResponseDto
{
    public List<DeviceDto>? Items { get; set; }
    public int TotalItems { get; set; }
    public bool AnyDevicesForUser { get; set; }
}
