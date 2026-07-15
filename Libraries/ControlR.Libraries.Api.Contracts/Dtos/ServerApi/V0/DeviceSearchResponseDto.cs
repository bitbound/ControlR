namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V0;

public class DeviceSearchResponseDto
{
    public List<DeviceResponseDto>? Items { get; set; }
    public int TotalItems { get; set; }
}
