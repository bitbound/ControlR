namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi;

public record ServerCreateUserRequestDto(
  Guid TenantId,
  string UserName,
  string? Email,
  string? Password,
  IEnumerable<Guid>? RoleIds,
  IEnumerable<Guid>? TagIds);