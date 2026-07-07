namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi;

public record IssueCreateUserRequestDto(
  Guid TenantId,
  string UserName,
  string? Email,
  string? Password,
  IEnumerable<Guid>? RoleIds = null,
  IEnumerable<Guid>? TagIds = null);
