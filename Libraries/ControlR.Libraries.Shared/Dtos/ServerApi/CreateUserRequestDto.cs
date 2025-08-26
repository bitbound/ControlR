namespace ControlR.Libraries.Shared.Dtos.ServerApi;

public record CreateUserRequestDto(
  string UserName,
  string? Email,
  string? Password,
  IEnumerable<Guid>? RoleIds,
  IEnumerable<Guid>? TagIds);
