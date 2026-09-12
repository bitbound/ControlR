namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.UserGroups;

public record UserGroupDetailDto(
  Guid Id,
  string Name,
  string? Description,
  DateTimeOffset CreatedAt,
  IReadOnlyList<UserGroupMemberDto> Members);