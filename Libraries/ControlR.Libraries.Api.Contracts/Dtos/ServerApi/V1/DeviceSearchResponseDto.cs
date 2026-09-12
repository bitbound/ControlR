namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1;

public class DeviceSearchResponseDto
{
  public bool AnyDevicesForUser { get; set; }
  public DeviceSearchFilterCountsDto FilterCounts { get; set; } = new();
  public IReadOnlyList<DeviceResponseDto>? Items { get; set; }
  public int TotalItems { get; set; }
}
