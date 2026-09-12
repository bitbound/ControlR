namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.DeviceGroups;

public class DeviceGroupsResponseDto
{
  public IReadOnlyList<DeviceGroupDto> Items { get; set; } = [];
}