namespace ControlR.Libraries.Shared.Dtos.ServerApi;
public record UserRoleAddRequestDto(
  Guid UserId,
  Guid RoleId);