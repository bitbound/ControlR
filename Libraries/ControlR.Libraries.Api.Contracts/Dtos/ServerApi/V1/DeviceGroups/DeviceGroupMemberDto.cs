namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.DeviceGroups;

public record DeviceGroupMemberDto(
  Guid DeviceId,
  string DeviceName,
  string? Alias,
  string? CustomerName);