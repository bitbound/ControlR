namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1;

public class DeviceSearchResponseDto
{
  public List<DeviceResponseDto>? Items { get; set; }
  public int TotalItems { get; set; }
}
