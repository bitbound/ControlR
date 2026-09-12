namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.UserGroups;

public record UserGroupDto(
  Guid Id,
  string Name,
  string? Description,
  DateTimeOffset CreatedAt,
  int MemberCount);