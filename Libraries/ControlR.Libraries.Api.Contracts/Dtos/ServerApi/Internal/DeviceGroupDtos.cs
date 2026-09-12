namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

public record DeviceGroupMemberDto(
  Guid DeviceId,
  string DeviceName,
  string? Alias,
  string? CustomerName);

public record DeviceGroupDto(
  Guid Id,
  string Name,
  string? Description,
  DateTimeOffset CreatedAt,
  int MemberCount);

public record DeviceGroupDetailDto(
  Guid Id,
  string Name,
  string? Description,
  DateTimeOffset CreatedAt,
  IReadOnlyList<DeviceGroupMemberDto> Members);
