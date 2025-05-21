using ControlR.Libraries.Shared.Enums;

namespace ControlR.Libraries.Shared.Dtos.ServerApi;

public class DeviceGridResponseDto
{
    public List<DeviceDto>? Items { get; set; }
    public int TotalItems { get; set; }
}
