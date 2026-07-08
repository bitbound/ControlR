namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V0;

public record CreateTenantRequestDto(
  string Name,
  string UserName,
  string? Email,
  string? Password);