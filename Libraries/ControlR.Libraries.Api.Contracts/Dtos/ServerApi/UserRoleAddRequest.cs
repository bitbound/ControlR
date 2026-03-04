namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi;
public record UserRoleAddRequestDto(
  Guid UserId,
  Guid RoleId);