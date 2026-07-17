namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;
public record UserRoleAddRequestDto(
  Guid UserId,
  Guid RoleId);