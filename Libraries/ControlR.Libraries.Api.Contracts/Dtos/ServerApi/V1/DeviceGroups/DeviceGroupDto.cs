namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.DeviceGroups;

public record DeviceGroupDto(
  Guid Id,
  string Name,
  string? Description,
  DateTimeOffset CreatedAt,
  int MemberCount);