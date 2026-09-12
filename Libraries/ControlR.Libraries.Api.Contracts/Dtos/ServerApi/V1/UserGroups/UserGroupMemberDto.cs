namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.UserGroups;

public record UserGroupMemberDto(
  Guid UserId,
  string UserName,
  string? DisplayName,
  DateTimeOffset? LastLogin);