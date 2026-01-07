namespace ControlR.Libraries.Shared.Dtos.ServerApi;

public class DeviceSearchResponseDto
{
    public bool AnyDevicesForUser { get; set; }
    public List<DeviceResponseDto>? Items { get; set; }
    public int TotalItems { get; set; }
}
