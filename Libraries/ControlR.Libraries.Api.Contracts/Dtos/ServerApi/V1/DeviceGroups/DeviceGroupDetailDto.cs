namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.DeviceGroups;

public record DeviceGroupDetailDto(
  Guid Id,
  string Name,
  string? Description,
  DateTimeOffset CreatedAt,
  IReadOnlyList<DeviceGroupMemberDto> Members);