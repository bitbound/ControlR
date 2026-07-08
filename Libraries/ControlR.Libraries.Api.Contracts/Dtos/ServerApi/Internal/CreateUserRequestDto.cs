namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

public record CreateUserRequestDto(
  string UserName,
  string? Email,
  string? Password,
  IEnumerable<Guid>? RoleIds,
  IEnumerable<Guid>? TagIds);
