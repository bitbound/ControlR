namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi;

public record CreateTenantRequestDto(
  string Name,
  string UserName,
  string? Email,
  string? Password);